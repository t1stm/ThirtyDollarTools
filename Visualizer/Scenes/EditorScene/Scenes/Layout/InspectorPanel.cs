using EditorScene.State;
using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     Context-sensitive right-side inspector: project + selected-track properties on
///     the arrangement, selected segment + note properties in the note editor. Pure
///     view - every edit routes through <see cref="EditorState" />. Structure rebuilds
///     on selection/mode changes (<see cref="Rebuild" />); values refresh in place on
///     model changes (<see cref="Sync" />), skipping the focused input so typing is
///     never interrupted by its own change events. Row building itself lives in
///     <see cref="InspectorForm" />; this class only decides what each section contains.
/// </summary>
public sealed partial class InspectorPanel
{
    public const float PanelWidth = 300f; // must match inspector-column's width in EditorInterface.snx.ss

    internal const float StatusBarHeight = 40f;
    internal const float RuleHeight = 1f;

    private readonly UIContext _context;

    private readonly InspectorForm _form;

    private readonly EditorState _state;

    /// <summary>The automation layout the rows were built for; see <see cref="Sync" />.</summary>
    private int _automationShape;

    /// <summary>A rebuild held back while a field was being typed in; see <see cref="RebuildWhenIdle" />.</summary>
    private bool _rebuildPending;

    private string? _syncedStatusLabel = "Idle"; // matches InspectorShell.snx.xml's constructed default
    private float _syncedStatusProgress = -1f; // never a valid Progress value, forces the first real SetStatus to apply

    /// <summary>
    ///     Drives the chrome built by <c>InspectorShell.snx.xml</c> - this class owns no tree
    ///     of its own, only the handles that document's logic resolved. Standalone callers
    ///     (the test suite) build the shell themselves and hand the same four elements over.
    /// </summary>
    public InspectorPanel(UIContext context, EditorState state,
        Panel element, ScrollView rows, ProgressBar statusBar, Label statusLabel)
    {
        _context = context;
        _state = state;
        Element = element;
        Rows = rows;
        StatusBar = statusBar;
        StatusLabelElement = statusLabel;
        _form = new InspectorForm(context, state, Rows);
        Rebuild();
    }

    /// <summary>The shell's root panel - what a host attaches, sizes and lays out.</summary>
    public Panel Element { get; }

    // Test seams (internal - see EditorAssembly's InternalsVisibleTo("EditorScene.Tests")).
    internal ScrollView Rows { get; }

    internal ProgressBar StatusBar { get; }

    internal Label StatusLabelElement { get; }

    /// <summary>
    ///     Fired when the user wants to edit a <see cref="TrackAutomation" />'s sound
    ///     filter. The inspector has no sound picker/modal of its own - EditorInterface
    ///     wires this the same way it wires <c>TrackEditorView.OnPreviewNote</c>.
    /// </summary>
    public Action<TrackAutomation>? OnEditTrackAutomationSounds { get; set; }

    /// <summary>
    ///     Fired when the user wants to reassign the selected note(s)' instrument (one
    ///     for a single selection, several for a multi-selection). The inspector has no
    ///     instrument selector of its own - EditorInterface wires this the same way it
    ///     wires <see cref="OnEditTrackAutomationSounds" />.
    /// </summary>
    public Action<IReadOnlyList<Note>>? OnReassignInstrument { get; set; }

    /// <summary>
    ///     Fired when the user wants to point a wave track at another file. Wired like
    ///     <see cref="OnChangeTrackColor" /> - the inspector owns no file dialog.
    /// </summary>
    public Action<WaveTrack>? OnReplaceWaveFile { get; init; }

    /// <summary>
    ///     Fired when the user wants to recolor the selected track(s) - one for a single
    ///     selection, several for a multi-selection, as <see cref="OnReassignInstrument" />
    ///     does. Same seam as the two above: EditorInterface owns the swatch dialog, this
    ///     panel only offers the row.
    /// </summary>
    public Action<IReadOnlyList<ProjectTrack>>? OnChangeTrackColor { get; set; }

    /// <summary>
    ///     The clip color a track currently paints with, for the Color row's chip. Supplied
    ///     by the host because the palette lives on the arrangement view (its stylesheet
    ///     owns it); unset, the chip renders transparent.
    /// </summary>
    public Func<ProjectTrack, Vector4>? TrackColor { get; init; }

    /// <summary>
    ///     Updates the status bar; null label shows "Idle" and hides the progress bar.
    ///     <paramref name="total" /> greater than zero appends the encoder's "done - total" counts
    ///     in brackets (e.g. "Rendering audio… (6 - 67)"); zero - the placement/mixing stages and a
    ///     fully-cached incremental render report nothing - leaves the label bare.
    ///     Called every frame from <see cref="EditorInterface.Update" /> - only touches elements
    ///     when the values actually changed, so it never dirties layout for nothing.
    /// </summary>
    public void SetStatus(string? label, float progress, ulong done = 0, ulong total = 0)
    {
        var text = label == null ? "Idle" : total > 0 ? $"{label} ({done} - {total})" : label;
        if (text != _syncedStatusLabel)
        {
            _syncedStatusLabel = text;
            StatusLabelElement.SetTextContents(text);
        }

        // The bar is built hidden; the Visible setter re-queues/dequeues its planes at the
        // current layer (Sundex.Components.Tests.ProgressBarVisibilityToggleTests).
        var barVisible = label != null;
        StatusBar.Visible = barVisible;

        if (!barVisible || Equals(progress, _syncedStatusProgress)) return;
        _syncedStatusProgress = progress;
        StatusBar.Progress = progress;
    }

    /// <summary>The input element showing a field, keyed "Section.Label" (e.g. "Track.Name").</summary>
    public UIElement? Field(string key)
    {
        return _form.Field(key);
    }

    /// <summary>Rebuilds the rows for the current mode and selection.</summary>
    public void Rebuild()
    {
        _rebuildPending = false;
        _automationShape = AutomationShape();
        foreach (var child in Rows.Children.ToArray()) Rows.RemoveChild(child);
        _form.Reset();

        if (_state.OpenedFaithfulTrack is { } faithful)
        {
            FaithfulSection(faithful);
        }
        else if (_state.OpenedTrack != null)
        {
            if (_state.SelectedSegment is { } segment)
            {
                var addSegment = new Button(_context, "+ Add")
                {
                    Classes = ["chip-button"],
                    OnClick = _ =>
                    {
                        if (_state.OpenedTrack is { } track) _state.SelectSegment(_state.AddSegment(track));
                    }
                };
                var removeSegment = new Button(_context, "− Remove")
                {
                    Classes = ["chip-button"],
                    OnClick = _ =>
                    {
                        // RemoveSegment refuses on the last segment (library invariant) - just a no-op here.
                        if (_state is { OpenedTrack: { } track, SelectedSegment: { } selected })
                            _state.RemoveSegment(track, selected);
                    }
                };
                _form.Header("Segment", addSegment, removeSegment);
                _form.IntRow("Numerator", () => segment.Numerator, v => segment.Numerator = v, 1, 64);
                _form.IntRow("Denominator", () => segment.Denominator, v => segment.Denominator = v, 1, 64);
                _form.IntRow("Bars", () => segment.Bars, v => segment.Bars = v, 1, 1024);
                _form.IntRow("Steps/beat", () => segment.StepsPerBeat, v => segment.StepsPerBeat = v, 1, 64);
                _form.NumberRow("BPM", () => segment.BPM, v => segment.BPM = (float?)v, 1, 9999, allowNull: true);
            }

            if (_state.SelectedNotes.Count > 1)
            {
                MultiNoteSection(_state.SelectedNotes);
            }
            else if (_state.SelectedNote is { } note)
            {
                if (note.IsCut)
                {
                    // Value/volume/pan/offset/automation are meaningless (always default)
                    // for a cut - Change stays, to retarget which instrument it cuts.
                    _form.Header("!cut event");
                    _form.InfoRow("Cuts", () => note.Instrument.Name);
                    _form.ActionRow("Change", () => OnReassignInstrument?.Invoke([note]));
                    _form.InfoRow("Step", () => note.Step.ToString());
                }
                else
                {
                    _form.Header("Note");
                    _form.InfoRow("Instrument", () => note.Instrument.Name);
                    _form.ActionRow("Change", () => OnReassignInstrument?.Invoke([note]));
                    _form.NumberRow("Value", () => note.Value, v => note.Value = v!.Value,
                        -TrackEditorView.MaxValue, TrackEditorView.MaxValue);
                    _form.NumberRow("Volume", () => note.Volume, v => note.Volume = v, 0, 500, 5, true);
                    _form.NumberRow("Pan", () => note.Pan, v => note.Pan = (float)v!.Value, -100, 100, 10);
                    _form.NumberRow("Offset (s)", () => note.Offset, v => note.Offset = v!.Value, -60, 60, 0.05);
                    AutomationSection(note);
                }
            }
        }
        else
        {
            _form.Header("Project");
            _form.TextRow("Name", () => _state.Project.Info.Name, v => _state.Edit(() => _state.Project.Info.Name = v));
            _form.TextRow("Author", () => _state.Project.Info.Author ?? "",
                v => _state.Edit(() => _state.Project.Info.Author = NullIfEmpty(v)));
            _form.TextRow("Description", () => _state.Project.Info.Description ?? "",
                v => _state.Edit(() => _state.Project.Info.Description = NullIfEmpty(v)));
            _form.NumberRow("BPM", () => _state.Project.RootTiming.BPM,
                v => _state.Project.RootTiming.BPM = (float)v!.Value, 1, 9999);
            _form.NumberRow("Transpose", () => _state.Project.Transpose,
                v => _state.Project.Transpose = (float)v!.Value,
                -TrackEditorView.MaxValue, TrackEditorView.MaxValue, 0.1);
            // A cut is by sound name, so one note's retriggers silence every other note on
            // the same sounds. On, the editor puts them back where they were cut.
            _form.CheckRow("Auto resume", () => _state.Project.AutoResume,
                resume => _state.Edit(() => _state.Project.AutoResume = resume));

            if (_state.SelectedPlacements.Count > 1)
            {
                MultiPlacementSection(_state.SelectedPlacements);
            }
            else if (_state.SelectedTracks.Count > 1)
            {
                // Name, tempo and automation are all per track; color is the one that
                // means something applied to the whole group at once.
                _form.Header($"Tracks (× {_state.SelectedTracks.Count})");
                ColorRowFor(_state.SelectedTracks);
            }
            else if (_state.SelectedTrack is WaveTrack wave)
            {
                WaveSection(wave);
            }
            else if (_state.SelectedTrack is { } track)
            {
                _form.Header("Track");
                _form.TextRow("Name", () => track.Name, v => _state.RenameTrack(track, v));
                _form.ColorRow("Color", () => TrackColor?.Invoke(track) ?? default,
                    () => OnChangeTrackColor?.Invoke([track]));
                _form.CheckRow("Project tempo", () => _state.TrackFollowsRootTiming(track), follows =>
                {
                    _state.SetTrackFollowsRootTiming(track, follows);
                    Rebuild(); // the own-BPM row appears/disappears
                });
                if (!_state.TrackFollowsRootTiming(track))
                    _form.NumberRow("BPM", () => track.Timing.BPM, v => track.Timing.BPM = (float)v!.Value, 1, 9999);

                TrackAutomationSection(track);
            }
        }

        Element.InvalidateLayout();
    }

    private void EditAndRebuild(Action edit)
    {
        _state.Edit(edit);
        RebuildWhenIdle();
    }

    /// <summary>
    ///     Rebuilds now, or once the field being typed in lets go. A rebuild throws the
    ///     focused field away with whatever is half typed in it - "1." on the way to "1.5"
    ///     commits 1 and would lose the field - so the edit is committed live and only the
    ///     rows it adds or removes wait. <see cref="Update" /> runs the held rebuild.
    /// </summary>
    private void RebuildWhenIdle()
    {
        if (IsTyping()) _rebuildPending = true;
        else Rebuild();
    }

    private bool IsTyping()
    {
        for (var element = _context.FocusedElement; element != null; element = element.Parent)
            if (ReferenceEquals(element, Rows))
                return true;
        return false;
    }

    /// <summary>Per frame: runs a rebuild held back while a field was focused, once none is.</summary>
    public void Update()
    {
        if (_rebuildPending && !IsTyping()) Rebuild();
    }

    /// <summary>Writes the model values into the rows. Call on any model change.</summary>
    public void Sync()
    {
        // A border or marker drag on the grid changes which automation rows belong on the
        // panel - a note can gain an automation, lose it, or grow a keyframe of its own -
        // and none of that goes through the panel, so the form would otherwise stay as it
        // was built.
        if (AutomationShape() != _automationShape)
        {
            RebuildWhenIdle();
            if (!_rebuildPending) return; // rebuilt, and built rows start in sync
        }

        _form.Sync();
    }

    /// <summary>
    ///     What the automation rows are built from: whether each selected note has an
    ///     automation at all, how many keyframes it holds, and which of them carry settings
    ///     of their own. Walks only the selection, so it is bounded by what is on the panel.
    /// </summary>
    private int AutomationShape()
    {
        var shape = new HashCode();
        foreach (var note in _state.SelectedNotes)
        {
            if (note.Automation is not { } automation)
            {
                shape.Add(-1);
                continue;
            }

            shape.Add(automation.Keyframes.Count);
            for (var i = 0; i < automation.Keyframes.Count; i++)
                if (!automation.Keyframes[i].ValueEquals(automation.Template))
                    shape.Add(i);
        }

        return shape.ToHashCode();
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
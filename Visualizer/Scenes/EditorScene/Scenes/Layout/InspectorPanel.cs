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
public sealed class InspectorPanel
{
    public const float PanelWidth = 300f; // must match inspector-column's width in EditorInterface.snx.ss

    internal const float StatusBarHeight = 40f;
    internal const float RuleHeight = 1f;

    private readonly UIContext _context;

    private readonly InspectorForm _form;

    private readonly EditorState _state;

    /// <summary>The automation layout the rows were built for; see <see cref="Sync" />.</summary>
    private int _automationShape;

    private string? _syncedStatusLabel = "Idle"; // matches InspectorShell.snx.xml's constructed default
    private float _syncedStatusProgress = -1f; // never a valid Progress value, forces the first real SetStatus to apply

    /// <summary>
    ///     The faithful editor's inspector: the selected slot's fields, or the track's own
    ///     summary when nothing is selected. There are no segments here - position is the
    ///     item's index, so the grid rows the piano roll shows would edit nothing.
    /// </summary>
    private void FaithfulSection(FaithfulTrack track)
    {
        if (_state.SelectedItem?.Note is { } note)
        {
            _form.Header("Sound");
            _form.InfoRow("Instrument", () => note.Instrument.Name);
            _form.ActionRow("Change", () => OnReassignInstrument?.Invoke([note]));
            _form.NumberRow("Value", () => note.Value, v => note.Value = v!.Value,
                -TrackEditorView.MaxValue, TrackEditorView.MaxValue);
            _form.NumberRow("Volume", () => note.Volume, v => note.Volume = v, 0, 500, 5, true);
            _form.NumberRow("Pan", () => note.Pan, v => note.Pan = (float)v!.Value, -100, 100, 10);
            _form.NumberRow("Offset (s)", () => note.Offset, v => note.Offset = v!.Value, -60, 60, 0.05);
            return;
        }

        if (_state.SelectedItem?.Action is { } action)
        {
            _form.Header(action.SoundEvent ?? "Action");
            _form.NumberRow("Value", () => action.Value, v =>
            {
                action.Value = v!.Value;
                action.WorkingValue = action.Value; // loop counters read the working copy
            }, -1e6, 1e6, 1);
            // The event as it will be written out - the only readout that shows a value
            // scale ("@x") or a packed "!pulse"/"!bg" payload for what it is.
            _form.InfoRow("Event", action.Stringify);
            return;
        }

        _form.Header("Faithful track");
        _form.TextRow("Name", () => track.Name, v => _state.RenameTrack(track, v));
        _form.InfoRow("Items", () => track.Items.Count.ToString());
        _form.InfoRow("Length", () => $"{track.DurationMinutes() * 60:0.##} s");
    }

    /// <summary>
    ///     A wave reference track's fields. No tempo and no automation - the file plays at its
    ///     own rate whatever the project does - and no segments, since there is nothing in it
    ///     to edit. "Missing" marks a path that no longer resolves: the clip keeps its saved
    ///     length and draws, it just has nothing to play.
    /// </summary>
    private void WaveSection(WaveTrack track)
    {
        _form.Header("Wave track");
        _form.TextRow("Name", () => track.Name, v => _state.RenameTrack(track, v));
        _form.ColorRow("Color", () => TrackColor?.Invoke(track) ?? default,
            () => OnChangeTrackColor?.Invoke([track]));
        _form.InfoRow("File", () => track.Path.Length == 0
            ? "None"
            : System.IO.Path.GetFileName(track.Path) + (File.Exists(track.Path) ? "" : " (missing)"));
        _form.ActionRow("Replace", () => OnReplaceWaveFile?.Invoke(track));
        _form.InfoRow("Length", () => $"{track.DurationSeconds:0.##} s");
        // Up to 8x, not the 2x a note gets: a note is played from a Thirty Dollar sound, which
        // is mastered loud (a bleep is about -9 dBFS RMS), while a reference is usually one stem
        // of a finished mix - the MOON "other" stem is -21 dBFS RMS, and several notes sounding
        // together put the song further above it still. Bridging that needs +15 dB or so, and
        // +6 dB was nowhere near it.
        _form.NumberRow("Volume", () => track.Volume, v => track.Volume = v!.Value, 0, 800, 5);
    }

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
    public Action<WaveTrack>? OnReplaceWaveFile { get; set; }

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
    public Func<ProjectTrack, Vector4>? TrackColor { get; set; }

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

    /// <summary>
    ///     Multi-note selection: independent modifier properties (Value/Volume/Pan/
    ///     Offset/Instrument) are always editable - uniform values show, differing ones
    ///     render empty and committing applies the absolute value to every selected
    ///     note. Automation is editable only when every note's is uniform (see
    ///     <see cref="MultiAutomationSection" />).
    /// </summary>
    private void MultiNoteSection(IReadOnlyList<Note> notes)
    {
        var primary = notes[^1]; // last = primary, per EditorState's selection-order convention

        _form.Header($"Note (× {notes.Count})");
        _form.InfoRow("Instrument", () => AllEqual(notes, n => n.Instrument) ? primary.Instrument.Name : "mixed");
        _form.ActionRow("Change", () => OnReassignInstrument?.Invoke(notes));

        // A mixed cut/normal selection edits its non-cut notes only - a cut's value/
        // volume/pan/offset are fixed invariants (see Note.IsCut).
        _form.NumberRow("Value", () => primary.Value,
            v =>
            {
                foreach (var n in notes)
                    if (!n.IsCut)
                        n.Value = v!.Value;
            },
            -TrackEditorView.MaxValue, TrackEditorView.MaxValue,
            mixed: () => !AllEqual(notes, n => n.Value));
        _form.NumberRow("Volume", () => primary.Volume,
            v =>
            {
                foreach (var n in notes)
                    if (!n.IsCut)
                        n.Volume = v;
            },
            0, 500, 5, true, () => !AllEqual(notes, n => n.Volume));
        _form.NumberRow("Pan", () => primary.Pan,
            v =>
            {
                foreach (var n in notes)
                    if (!n.IsCut)
                        n.Pan = (float)v!.Value;
            },
            -100, 100, 10, mixed: () => !AllEqual(notes, n => n.Pan));
        _form.NumberRow("Offset (s)", () => primary.Offset,
            v =>
            {
                foreach (var n in notes)
                    if (!n.IsCut)
                        n.Offset = v!.Value;
            },
            -60, 60, 0.05, mixed: () => !AllEqual(notes, n => n.Offset));

        MultiAutomationSection(notes, primary);
    }

    /// <summary>
    ///     Uniform means all null, or all non-null and structurally equal
    ///     (<see cref="AudioKeyframeManager.ValueEquals" />). All-null offers "+ Add
    ///     automation", giving each note its own manager instance - never shared, matching
    ///     <see cref="Note.Duplicate" />. Uniform renders the full form bound to the primary
    ///     note and clones it out to the rest on every commit. Mixed shows one disabled
    ///     info row.
    /// </summary>
    private void MultiAutomationSection(IReadOnlyList<Note> notes, Note primary)
    {
        _form.Header("Automation");

        if (notes.All(n => n.Automation == null))
        {
            _form.ActionRow("+ Add automation", () => EditAndRebuild(() =>
            {
                foreach (var note in notes)
                    if (!note.IsCut)
                        note.Automation = new AudioKeyframeManager();
            }));
            return;
        }

        if (notes.Any(n => n.Automation == null) ||
            !notes.All(n => n.Automation!.ValueEquals(primary.Automation!)))
        {
            _form.InfoRow("Automation", () => "mixed - select notes with matching automation to edit");
            return;
        }

        KeyframeBlocks("Automation", "", primary.Automation!, () => FanOutAutomation(notes, primary));
        _form.Section = "Automation";
        _form.ActionRow("Remove automation", () => EditAndRebuild(() =>
        {
            foreach (var note in notes) note.Automation = null;
        }));
    }

    private static void FanOutAutomation(IReadOnlyList<Note> notes, Note primary)
    {
        foreach (var note in notes)
            if (note != primary && !note.IsCut)
                note.Automation = primary.Automation!.Clone();
    }

    /// <summary>
    ///     Multi-placement selection: placements own only position, never inspector-
    ///     edited - the Track section (name/tempo/BPM/track automation) shows only when
    ///     every selected placement references the same <see cref="ProjectTrack" />.
    /// </summary>
    private void MultiPlacementSection(IReadOnlyList<TrackPlacement> placements)
    {
        _form.Header($"Clips (× {placements.Count})");
        _form.Header("Track");

        if (!AllEqual(placements, p => p.Track))
        {
            _form.InfoRow("Track", () => "mixed");
            // Color is the one track property a mixed selection can still set: it is per
            // track, but picking one swatch for all of them is exactly what grouping a
            // section of an arrangement by color means.
            ColorRowFor([.. placements.Select(p => p.Track).Distinct()]);
            return;
        }

        var track = placements[0].Track;
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

    /// <summary>
    ///     A Color row for a group of tracks: the chip shows their shared fill, or nothing
    ///     when they differ - the counterpart of the multi-note rows' "mixed" state - and
    ///     the picked swatch applies to every track in the group.
    /// </summary>
    private void ColorRowFor(IReadOnlyList<ProjectTrack> tracks)
    {
        if (tracks.Count == 0) return;

        _form.ColorRow("Color",
            () => AllEqual(tracks, t => t.ColorIndex) ? TrackColor?.Invoke(tracks[0]) ?? default : default,
            () => OnChangeTrackColor?.Invoke(tracks));
    }

    private static bool AllEqual<TItem, TValue>(IReadOnlyList<TItem> items, Func<TItem, TValue> selector)
    {
        var first = selector(items[0]);
        return items.All(item => Equals(selector(item), first));
    }

    /// <summary>
    ///     Form for <see cref="Note.Automation" />: each keyframe fires one generated event,
    ///     its gap after the previous one, modifying the previous result. Structural edits
    ///     (add/remove) rebuild; field edits sync like every other row.
    /// </summary>
    private void AutomationSection(Note note)
    {
        _form.Header("Automation");
        if (note.Automation is not { } automation)
        {
            _form.ActionRow("+ Add automation",
                () => EditAndRebuild(() => note.Automation = new AudioKeyframeManager()));
            return;
        }

        KeyframeBlocks("Automation", "", automation);
        _form.Section = "Automation";
        _form.ActionRow("Remove automation", () => EditAndRebuild(() => note.Automation = null));
    }

    /// <summary>
    ///     The same form for a whole track: any number of automations, each with its own
    ///     sound filter (null = every sound), instead of one note's single nullable
    ///     <see cref="Note.Automation" />. Shares <see cref="KeyframeBlocks" /> with
    ///     <see cref="AutomationSection" /> for the gap/repeats/keyframe rows.
    /// </summary>
    private void TrackAutomationSection(ProjectTrack track)
    {
        _form.Header("Track Automation");

        _form.NumberRow("Transpose", () => track.Transpose, v => track.Transpose = (float?)v,
            -TrackEditorView.MaxValue, TrackEditorView.MaxValue, 0.1, true);

        for (var i = 0; i < track.TrackAutomations.Count; i++)
        {
            var entry = track.TrackAutomations[i];
            var section = $"Track Automation {i + 1}";

            _form.Card("inspector-card-entry", () =>
            {
                _form.Header(section);

                _form.CheckRow("All sounds", () => entry.Sounds is null,
                    allSounds => EditAndRebuild(() => entry.Sounds = allSounds ? null : []));
                if (entry.Sounds is { } sounds)
                    _form.ActionRow("Sounds", $"Sounds: {sounds.Count} selected",
                        () => OnEditTrackAutomationSounds?.Invoke(entry));

                KeyframeBlocks(section, $"{section} ", entry.Keyframes);

                _form.Section = section;
                _form.ActionRow("Remove", () => EditAndRebuild(() => track.RemoveTrackAutomation(entry)));
            });
        }

        _form.Section = "Track Automation";
        _form.ActionRow("+ Add automation",
            () => EditAndRebuild(() => track.AddTrackAutomation(new AudioKeyframeManager())));
    }

    /// <summary>
    ///     Timing, the note's length and retrigger grid, the cut flags, and one card per
    ///     keyframe - shared by note and track automation. Keyframes are generated from
    ///     Gap/End/AutomationOffset, so there is nothing to add or remove by hand.
    ///     <paramref name="keyframeHeaderPrefix" /> disambiguates keyframe headers when
    ///     several automations are on screen at once (empty for the single per-note
    ///     automation, so its field keys are unchanged: "Keyframe 1.Value").
    ///     <paramref name="afterEdit" />, when given, runs after every commit (field or
    ///     structural) - the multi-note form's clone-fan-out hook (see
    ///     <see cref="MultiAutomationSection" />); null for every other caller.
    /// </summary>
    private void KeyframeBlocks(string section, string keyframeHeaderPrefix, AudioKeyframeManager automation,
        Action? afterEdit = null)
    {
        void Commit(Action edit)
        {
            _state.Edit(edit);
            afterEdit?.Invoke();
        }

        _form.Section = section;
        _form.CheckRow("Gaps in seconds", () => automation.Timing == KeyframeTiming.Time,
            timeMode =>
            {
                _state.Edit(() => automation.Timing = timeMode ? KeyframeTiming.Time : KeyframeTiming.Step);
                afterEdit?.Invoke();
            });
        // Gap and End resize the keyframe list, so their cards appear and disappear.
        _form.NumberRow("Gap", () => automation.Gap, v => EditAndRebuild(() =>
        {
            automation.Gap = (float)v!.Value;
            afterEdit?.Invoke();
        }), 0, 4096, 0.5);
        _form.NumberRow("End", () => automation.End, v => EditAndRebuild(() =>
        {
            automation.End = (float)v!.Value;
            afterEdit?.Invoke();
        }), 0, 4096, 1);
        // "Grid offset", not "Automation offset": the label column is 84 px and the longer
        // name clips. It still reads apart from the per-keyframe sound Offset below.
        _form.NumberRow("Grid offset", () => automation.AutomationOffset, v => EditAndRebuild(() =>
        {
            automation.AutomationOffset = (float)v!.Value;
            afterEdit?.Invoke();
        }), -4096, 4096, 0.5);
        _form.CheckRow("Cut", () => automation.Cut, cut => EditAndRebuild(() =>
        {
            automation.Cut = cut;
            afterEdit?.Invoke();
        }), [("Cut at end", () => automation.CutAtEnd, v => EditAndRebuild(() =>
        {
            automation.CutAtEnd = v;
            afterEdit?.Invoke();
        }))]);

        // The keyframe list can be hundreds long, so one card edits them all: the template
        // every auto-created keyframe copies, fanned out to the ones that still match it.
        // Only keyframes given settings of their own get a card.
        void EditTemplate(Action<AudioKeyframe> apply)
        {
            var followers = automation.Keyframes.Where(k => k.ValueEquals(automation.Template)).ToList();
            _state.Edit(() =>
            {
                apply(automation.Template);
                foreach (var keyframe in followers) apply(keyframe);
            });
            afterEdit?.Invoke();
        }

        // Nothing to shape until the note has a length to hold keyframes.
        if (automation.Keyframes.Count > 0)
            _form.Card("inspector-card-keyframe", () =>
        {
            _form.Header($"{keyframeHeaderPrefix}All keyframes  (× {automation.Keyframes.Count})");
            _form.Section = $"{keyframeHeaderPrefix}All keyframes";
            KeyframeFields(automation.Template, EditTemplate, () => EditTemplate(k => k.Position = null));
            // Auto offset splices onto the instance it replaces, which only works if that
            // instance is cut - so ticking it turns the automation's cut on and keeps it.
            _form.CheckRow("Auto offset", () => automation.Template.AutoOffset, auto => EditAndRebuild(() =>
            {
                EditTemplate(k => k.AutoOffset = auto);
                if (auto) automation.Cut = true;
            }));
        });

        for (var i = 0; i < automation.Keyframes.Count; i++)
        {
            var keyframe = automation.Keyframes[i];
            if (keyframe.ValueEquals(automation.Template)) continue; // follows the card above

            var index = i;
            _form.Card("inspector-card-keyframe", () =>
            {
                _form.Header($"{keyframeHeaderPrefix}Keyframe {index + 1}  (edited)");
                _form.Section = $"{keyframeHeaderPrefix}Keyframe {index + 1}";
                _form.NumberRow("Position", () => keyframe.Position ?? automation.PositionOf(index),
                    v => Commit(() => keyframe.Position = (float?)v), 0, 4096, 0.25, true);
                KeyframeFields(keyframe, apply => Commit(() => apply(keyframe)), null);
                _form.CheckRow("Auto offset", () => keyframe.AutoOffset, auto => EditAndRebuild(() =>
                {
                    keyframe.AutoOffset = auto;
                    if (auto) automation.Cut = true;
                    afterEdit?.Invoke();
                }));
                _form.ActionRow("reset", () => EditAndRebuild(() =>
                {
                    var template = automation.Template.Clone(false);
                    keyframe.Position = null;
                    keyframe.Value = template.Value;
                    keyframe.Volume = template.Volume;
                    keyframe.Pan = template.Pan;
                    keyframe.Offset = template.Offset;
                    keyframe.AutoOffset = template.AutoOffset;
                    afterEdit?.Invoke();
                }));
            });
        }

        _form.Section = section;
        // Typing a keyframe's own values needs a card, and only an edited keyframe has one -
        // this pins the first one that still follows the template, the way dragging its
        // marker would.
        if (automation.Keyframes.Count > 0)
            _form.ActionRow("+ Edit a single keyframe...", () => EditAndRebuild(() =>
            {
                for (var i = 0; i < automation.Keyframes.Count; i++)
                {
                    if (!automation.Keyframes[i].ValueEquals(automation.Template)) continue;
                    automation.Keyframes[i].Position = automation.PositionOf(i);
                    break;
                }

                afterEdit?.Invoke();
            }));
    }

    /// <summary>
    ///     The four relative-change rows shared by the "All keyframes" card and an edited
    ///     keyframe's own. <paramref name="edit" /> applies one change - to the template and
    ///     its followers, or to the single keyframe.
    /// </summary>
    private void KeyframeFields(AudioKeyframe keyframe, Action<Action<AudioKeyframe>> edit, Action? resetPosition)
    {
        _ = resetPosition;
        _form.ModifierRow("Value", () => keyframe.Value, m => edit(k => k.Value = m));
        _form.ModifierRow("Volume", () => keyframe.Volume, m => edit(k => k.Volume = m));
        _form.ModifierRow("Pan", () => keyframe.Pan, m => edit(k => k.Pan = m));
        _form.ModifierRow("Offset", () => keyframe.Offset, m => edit(k => k.Offset = m));
    }

    private void EditAndRebuild(Action edit)
    {
        _state.Edit(edit);
        Rebuild();
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
            Rebuild();
            return;
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
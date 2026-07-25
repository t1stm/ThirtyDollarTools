using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Context-sensitive right-side inspector: project + selected-track properties on
///     the arrangement, selected segment + note properties in the note editor. Pure
///     view - every edit routes through <see cref="EditorState" />. Structure rebuilds
///     on selection/mode changes (<see cref="Rebuild" />); values refresh in place on
///     model changes (<see cref="Sync" />), skipping the focused input so typing is
///     never interrupted by its own change events. Row building itself lives in
///     <see cref="InspectorForm" />; this class only decides what each section contains.
/// </summary>
public sealed class InspectorPanel : Panel
{
    public const float PanelWidth = 300f; // must match inspector-column's width in EditorInterface.snx.ss

    internal const float StatusBarHeight = 40f;
    internal const float RuleHeight = 1f;
    private const float StatusProgressHeight = 6f;

    private static readonly Vector4 EntryColor = EditorPalette.Surface; // one shade above the panel background
    private static readonly Vector4 KeyframeColor = EditorPalette.SurfaceRaised; // one more shade up, nested inside an entry

    private readonly ScrollView _rows;
    private readonly InspectorForm _form;
    private readonly EditorState _state;
    private readonly Label _statusLabel;
    private readonly ProgressBar _statusBar;
    private readonly FlexPanel _statusSection;

    private string? _syncedStatusLabel = "Idle"; // matches the constructed default below
    private float _syncedStatusProgress = -1f; // never a valid Progress value, forces the first real SetStatus to apply

    public InspectorPanel(UIContext context, EditorState state) : base(context)
    {
        _state = state;
        _rows = new ScrollView(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Padding = 10,
            Spacing = 8
        };
        _form = new InspectorForm(context, state, _rows);

        var rule = new Panel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = RuleHeight,
            Background = new ColoredPlane { Color = EditorPalette.Divider }
        };

        _statusLabel = new Label(context, "Idle")
        {
            FontSizePx = 12f,
            Color = EditorPalette.TextMuted
        };
        _statusBar = new ProgressBar(context,
            new ColoredPlane { Color = EditorPalette.Surface },
            new ColoredPlane { Color = EditorPalette.Accent })
        {
            Width = LiteralOrComputable.Percent(100),
            Height = StatusProgressHeight,
            Visible = false
        };
        _statusSection = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Height = StatusBarHeight,
            Padding = 8,
            Spacing = 4,
            Children = [_statusLabel, _statusBar]
        };

        var body = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Children = [_rows, rule, _statusSection]
        };
        AddChild(body);
        Rebuild();
    }

    // Test seams (internal - see EditorAssembly's InternalsVisibleTo("EditorScene.Tests")).
    internal ScrollView Rows => _rows;
    internal ProgressBar StatusBar => _statusBar;
    internal Label StatusLabelElement => _statusLabel;
    internal FlexPanel StatusSection => _statusSection;

    /// <summary>Updates the status bar; null label shows "Idle" and hides the progress bar.
    /// <paramref name="total" /> greater than zero appends the encoder's "done - total" counts
    /// in brackets (e.g. "Rendering audio… (6 - 67)"); zero - the placement/mixing stages and a
    /// fully-cached incremental render report nothing - leaves the label bare.
    /// Called every frame from <see cref="EditorInterface.Update" /> - only touches elements
    /// when the values actually changed, so it never dirties layout for nothing.</summary>
    public void SetStatus(string? label, float progress, ulong done = 0, ulong total = 0)
    {
        var text = label == null ? "Idle" : total > 0 ? $"{label} ({done} - {total})" : label;
        if (text != _syncedStatusLabel)
        {
            _syncedStatusLabel = text;
            _statusLabel.SetTextContents(text);
        }

        // The bar is built hidden; the Visible setter re-queues/dequeues its planes at the
        // current layer (Sundex.Components.Tests.ProgressBarVisibilityToggleTests).
        var barVisible = label != null;
        _statusBar.Visible = barVisible;

        if (!barVisible || Equals(progress, _syncedStatusProgress)) return;
        _syncedStatusProgress = progress;
        _statusBar.Progress = progress;
    }

    /// <summary>The input element showing a field, keyed "Section.Label" (e.g. "Track.Name").</summary>
    public UIElement? Field(string key)
    {
        return _form.Field(key);
    }

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

    /// <summary>Rebuilds the rows for the current mode and selection.</summary>
    public void Rebuild()
    {
        foreach (var child in _rows.Children.ToArray()) _rows.RemoveChild(child);
        _form.Reset();

        if (_state.OpenedTrack != null)
        {
            if (_state.SelectedSegment is { } segment)
            {
                var addSegment = new Button(Context, "+ Add")
                {
                    FontSizePx = 11f,
                    BorderRadius = 6,
                    Background = new ColoredPlane { Color = EditorPalette.Surface },
                    OnClick = _ =>
                    {
                        if (_state.OpenedTrack is { } track) _state.SelectSegment(_state.AddSegment(track));
                    }
                };
                var removeSegment = new Button(Context, "− Remove")
                {
                    FontSizePx = 11f,
                    BorderRadius = 6,
                    Background = new ColoredPlane { Color = EditorPalette.Surface },
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
                    _form.NumberRow("Volume", () => note.Volume, v => note.Volume = v, 0, 500, 5, allowNull: true);
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
            _form.NumberRow("Transpose", () => (double?)_state.Project.Transpose,
                v => _state.Project.Transpose = (float)v!.Value,
                -TrackEditorView.MaxValue, TrackEditorView.MaxValue, 0.1);

            if (_state.SelectedPlacements.Count > 1)
            {
                MultiPlacementSection(_state.SelectedPlacements);
            }
            else if (_state.SelectedTrack is { } track)
            {
                _form.Header("Track");
                _form.TextRow("Name", () => track.Name, v => _state.RenameTrack(track, v));
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

        InvalidateLayout();
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
            v => { foreach (var n in notes) if (!n.IsCut) n.Value = v!.Value; },
            -TrackEditorView.MaxValue, TrackEditorView.MaxValue,
            mixed: () => !AllEqual(notes, n => n.Value));
        _form.NumberRow("Volume", () => primary.Volume,
            v => { foreach (var n in notes) if (!n.IsCut) n.Volume = v; },
            0, 500, 5, allowNull: true, mixed: () => !AllEqual(notes, n => n.Volume));
        _form.NumberRow("Pan", () => primary.Pan,
            v => { foreach (var n in notes) if (!n.IsCut) n.Pan = (float)v!.Value; },
            -100, 100, 10, mixed: () => !AllEqual(notes, n => n.Pan));
        _form.NumberRow("Offset (s)", () => primary.Offset,
            v => { foreach (var n in notes) if (!n.IsCut) n.Offset = v!.Value; },
            -60, 60, 0.05, mixed: () => !AllEqual(notes, n => n.Offset));

        MultiAutomationSection(notes, primary);
    }

    /// <summary>
    ///     Uniform means all null, or all non-null and structurally equal
    ///     (<see cref="AudioKeyframeManager.ValueEquals" />). All-null offers "+ Add
    ///     automation" (a separate manager instance per note, matching
    ///     <see cref="Note.Duplicate" />'s never-shared semantics). Uniform renders the
    ///     full form bound to the primary note; every commit clone-fans-out to the rest
    ///     (simpler and safer than mirroring individual field writes - the managers are
    ///     tiny). Mixed shows one disabled info row.
    /// </summary>
    private void MultiAutomationSection(IReadOnlyList<Note> notes, Note primary)
    {
        _form.Header("Automation");

        if (notes.All(n => n.Automation == null))
        {
            _form.ActionRow("+ Add automation", () => EditAndRebuild(() =>
            {
                foreach (var note in notes)
                    if (!note.IsCut) note.Automation = new AudioKeyframeManager();
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
            return;
        }

        var track = placements[0].Track;
        _form.TextRow("Name", () => track.Name, v => _state.RenameTrack(track, v));
        _form.CheckRow("Project tempo", () => _state.TrackFollowsRootTiming(track), follows =>
        {
            _state.SetTrackFollowsRootTiming(track, follows);
            Rebuild(); // the own-BPM row appears/disappears
        });
        if (!_state.TrackFollowsRootTiming(track))
            _form.NumberRow("BPM", () => track.Timing.BPM, v => track.Timing.BPM = (float)v!.Value, 1, 9999);

        TrackAutomationSection(track);
    }

    private static bool AllEqual<TItem, TValue>(IReadOnlyList<TItem> items, Func<TItem, TValue> selector)
    {
        var first = selector(items[0]);
        return items.All(item => Equals(selector(item), first));
    }

    /// <summary>
    ///     Phase-6 form for <see cref="Note.Automation" />: each keyframe fires one
    ///     generated event, its gap after the previous one, modifying the previous
    ///     result. Structural edits (add/remove) rebuild; field edits sync like every
    ///     other row.
    /// </summary>
    private void AutomationSection(Note note)
    {
        _form.Header("Automation");
        if (note.Automation is not { } automation)
        {
            _form.ActionRow("+ Add automation", () => EditAndRebuild(() => note.Automation = new AudioKeyframeManager()));
            return;
        }

        KeyframeBlocks("Automation", "", automation);
        _form.Section = "Automation";
        _form.ActionRow("Remove automation", () => EditAndRebuild(() => note.Automation = null));
    }

    /// <summary>
    ///     Phase-6-esque form for a whole track: any number of automations, each with its
    ///     own sound filter (null = every sound), instead of one note's single nullable
    ///     <see cref="Note.Automation" />. Shares <see cref="KeyframeBlocks" /> with
    ///     <see cref="AutomationSection" /> for the gap/repeats/keyframe rows.
    /// </summary>
    private void TrackAutomationSection(ProjectTrack track)
    {
        _form.Header("Track Automation");

        _form.NumberRow("Transpose", () => (double?)track.Transpose, v => track.Transpose = (float?)v,
            -TrackEditorView.MaxValue, TrackEditorView.MaxValue, 0.1, allowNull: true);

        for (var i = 0; i < track.TrackAutomations.Count; i++)
        {
            var entry = track.TrackAutomations[i];
            var section = $"Track Automation {i + 1}";

            _form.Card(EntryColor, () =>
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
    ///     Gaps-in-seconds checkbox, Repeats, and the per-keyframe rows - shared by note
    ///     and track automation. <paramref name="keyframeHeaderPrefix" /> disambiguates
    ///     keyframe headers when several automations are on screen at once (empty for the
    ///     single per-note automation, so its field keys are unchanged: "Keyframe 1.Gap").
    ///     <paramref name="afterEdit" />, when given, runs after every commit (field or
    ///     structural) - the multi-note form's clone-fan-out hook (see
    ///     <see cref="MultiAutomationSection" />); null for every other caller.
    /// </summary>
    private void KeyframeBlocks(string section, string keyframeHeaderPrefix, AudioKeyframeManager automation,
        Action? afterEdit = null)
    {
        _form.Section = section;
        _form.CheckRow("Gaps in seconds", () => automation.Timing == KeyframeTiming.Time,
            timeMode =>
            {
                _state.Edit(() => automation.Timing = timeMode ? KeyframeTiming.Time : KeyframeTiming.Step);
                afterEdit?.Invoke();
            });
        _form.IntRow("Repeats", () => automation.Repeats, v =>
        {
            automation.Repeats = v;
            afterEdit?.Invoke();
        }, 1, 1024);

        for (var i = 0; i < automation.Keyframes.Count; i++)
        {
            var keyframe = automation.Keyframes[i];
            _form.Card(KeyframeColor, () =>
            {
                _form.Header($"{keyframeHeaderPrefix}Keyframe {i + 1}");
                _form.NumberRow("Gap", () => keyframe.Gap, v =>
                {
                    keyframe.Gap = (float)v!.Value;
                    afterEdit?.Invoke();
                }, 0, 4096, 0.5);
                _form.CheckRow("Cut", () => keyframe.Cut, cut =>
                {
                    _state.Edit(() => keyframe.Cut = cut);
                    afterEdit?.Invoke();
                });
                _form.ModifierRow("Value", () => keyframe.Value, m =>
                {
                    keyframe.Value = m;
                    afterEdit?.Invoke();
                });
                _form.ModifierRow("Volume", () => keyframe.Volume, m =>
                {
                    keyframe.Volume = m;
                    afterEdit?.Invoke();
                });
                _form.ModifierRow("Pan", () => keyframe.Pan, m =>
                {
                    keyframe.Pan = m;
                    afterEdit?.Invoke();
                });
                _form.ModifierRow("Offset", () => keyframe.Offset, m =>
                {
                    keyframe.Offset = m;
                    afterEdit?.Invoke();
                });
                _form.ActionRow("Remove", () => EditAndRebuild(() =>
                {
                    automation.Keyframes.Remove(keyframe);
                    afterEdit?.Invoke();
                }));
            });
        }

        _form.Section = section;
        _form.ActionRow("+ Keyframe", () => EditAndRebuild(() =>
        {
            automation.Keyframes.Add(new AudioKeyframe());
            afterEdit?.Invoke();
        }));
    }

    private void EditAndRebuild(Action edit)
    {
        _state.Edit(edit);
        Rebuild();
    }

    /// <summary>Writes the model values into the rows. Call on any model change.</summary>
    public void Sync()
    {
        _form.Sync();
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

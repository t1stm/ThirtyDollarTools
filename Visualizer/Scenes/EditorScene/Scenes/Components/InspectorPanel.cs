using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Context-sensitive right-side inspector: project + selected-track properties on
///     the arrangement, selected segment + note properties in the note editor. Pure
///     view — every edit routes through <see cref="EditorState" />. Structure rebuilds
///     on selection/mode changes (<see cref="Rebuild" />); values refresh in place on
///     model changes (<see cref="Sync" />), skipping the focused input so typing is
///     never interrupted by its own change events. Row building itself lives in
///     <see cref="InspectorForm" />; this class only decides what each section contains.
/// </summary>
public sealed class InspectorPanel : Panel
{
    public const float PanelWidth = 300f; // must match inspector-column's width in EditorInterface.snx.ss

    private static readonly Vector4 EntryColor = EditorPalette.Surface; // one shade above the panel background
    private static readonly Vector4 KeyframeColor = EditorPalette.SurfaceRaised; // one more shade up, nested inside an entry

    private readonly ScrollView _rows;
    private readonly InspectorForm _form;
    private readonly EditorState _state;

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
        AddChild(_rows);
        Rebuild();
    }

    /// <summary>The input element showing a field, keyed "Section.Label" (e.g. "Track.Name").</summary>
    public UIElement? Field(string key)
    {
        return _form.Field(key);
    }

    /// <summary>
    ///     Fired when the user wants to edit a <see cref="TrackAutomation" />'s sound
    ///     filter. The inspector has no sound picker/modal of its own — EditorInterface
    ///     wires this the same way it wires <c>TrackEditorView.OnPreviewNote</c>.
    /// </summary>
    public Action<TrackAutomation>? OnEditTrackAutomationSounds { get; set; }

    /// <summary>
    ///     Fired when the user wants to reassign the selected note's instrument. The
    ///     inspector has no instrument selector of its own — EditorInterface wires this
    ///     the same way it wires <see cref="OnEditTrackAutomationSounds" />.
    /// </summary>
    public Action<Note>? OnReassignInstrument { get; set; }

    /// <summary>Rebuilds the rows for the current mode and selection.</summary>
    public void Rebuild()
    {
        foreach (var child in _rows.Children.ToArray()) _rows.RemoveChild(child);
        _form.Reset();

        if (_state.OpenedTrack != null)
        {
            if (_state.SelectedSegment is { } segment)
            {
                _form.Header("Segment");
                _form.IntRow("Numerator", () => segment.Numerator, v => segment.Numerator = v, 1, 64);
                _form.IntRow("Denominator", () => segment.Denominator, v => segment.Denominator = v, 1, 64);
                _form.IntRow("Bars", () => segment.Bars, v => segment.Bars = v, 1, 1024);
                _form.IntRow("Steps/beat", () => segment.StepsPerBeat, v => segment.StepsPerBeat = v, 1, 64);
                _form.NumberRow("BPM", () => segment.BPM, v => segment.BPM = (float?)v, 1, 9999, allowNull: true);
            }

            if (_state.SelectedNote is { } note)
            {
                _form.Header("Note");
                _form.InfoRow("Instrument", () => note.Instrument.Name);
                _form.ActionRow("Change", () => OnReassignInstrument?.Invoke(note));
                _form.NumberRow("Value", () => note.Value, v => note.Value = v!.Value,
                    -TrackEditorView.MaxValue, TrackEditorView.MaxValue);
                _form.NumberRow("Volume", () => note.Volume, v => note.Volume = v, 0, 500, 5, allowNull: true);
                _form.NumberRow("Pan", () => note.Pan, v => note.Pan = (float)v!.Value, -100, 100, 10);
                _form.NumberRow("Offset (s)", () => note.Offset, v => note.Offset = v!.Value, -60, 60, 0.05);
                AutomationSection(note);
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

            if (_state.SelectedTrack is { } track)
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
    ///     Gaps-in-seconds checkbox, Repeats, and the per-keyframe rows — shared by note
    ///     and track automation. <paramref name="keyframeHeaderPrefix" /> disambiguates
    ///     keyframe headers when several automations are on screen at once (empty for the
    ///     single per-note automation, so its field keys are unchanged: "Keyframe 1.Gap").
    /// </summary>
    private void KeyframeBlocks(string section, string keyframeHeaderPrefix, AudioKeyframeManager automation)
    {
        _form.Section = section;
        _form.CheckRow("Gaps in seconds", () => automation.Timing == KeyframeTiming.Time,
            timeMode => _state.Edit(() =>
                automation.Timing = timeMode ? KeyframeTiming.Time : KeyframeTiming.Step));
        _form.IntRow("Repeats", () => automation.Repeats, v => automation.Repeats = v, 1, 1024);

        for (var i = 0; i < automation.Keyframes.Count; i++)
        {
            var keyframe = automation.Keyframes[i];
            _form.Card(KeyframeColor, () =>
            {
                _form.Header($"{keyframeHeaderPrefix}Keyframe {i + 1}");
                _form.NumberRow("Gap", () => keyframe.Gap, v => keyframe.Gap = (float)v!.Value, 0, 4096, 0.5);
                _form.CheckRow("Cut", () => keyframe.Cut, cut => _state.Edit(() => keyframe.Cut = cut));
                _form.ModifierRow("Value", () => keyframe.Value, m => keyframe.Value = m);
                _form.ModifierRow("Volume", () => keyframe.Volume, m => keyframe.Volume = m);
                _form.ModifierRow("Pan", () => keyframe.Pan, m => keyframe.Pan = m);
                _form.ModifierRow("Offset", () => keyframe.Offset, m => keyframe.Offset = m);
                _form.ActionRow("Remove", () => EditAndRebuild(() => automation.Keyframes.Remove(keyframe)));
            });
        }

        _form.Section = section;
        _form.ActionRow("+ Keyframe", () => EditAndRebuild(() => automation.Keyframes.Add(new AudioKeyframe())));
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

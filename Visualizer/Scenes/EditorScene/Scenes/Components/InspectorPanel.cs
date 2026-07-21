using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
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
///     never interrupted by its own change events.
/// </summary>
public sealed class InspectorPanel : Panel
{
    public const float PanelWidth = 300f; // must match inspector-column's width in EditorInterface.snx.ss
    private const float LabelWidth = 84f;
    private const float FieldWidth = 140f;
    private const float RowHeight = 34f;

    private static readonly Vector4 HeaderColor = new(0.478f, 0.635f, 0.968f, 1f); // #7aa2f7
    private static readonly Vector4 NameColor = new(0.337f, 0.373f, 0.537f, 1f); // #565f89
    private static readonly Vector4 EntryColor = new(0.16f, 0.18f, 0.26f, 1f); // #292e42, one shade above the #16161e panel
    private static readonly Vector4 KeyframeColor = new(0.21f, 0.23f, 0.33f, 1f); // #353a54, one more shade up, nested inside an entry
    private static readonly Vector4 InputColor = new(0.15f, 0.16f, 0.21f, 1f); // matches InstrumentEditor's name field

    private readonly Dictionary<string, UIElement> _fields = [];
    private readonly ScrollView _rows;
    private Panel _container;
    private readonly EditorState _state;
    private readonly List<Action> _syncs = [];
    private string _section = "";

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
        _container = _rows;
        AddChild(_rows);
        Rebuild();
    }

    /// <summary>The input element showing a field, keyed "Section.Label" (e.g. "Track.Name").</summary>
    public UIElement? Field(string key)
    {
        return _fields.GetValueOrDefault(key);
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
        _fields.Clear();
        _syncs.Clear();
        _container = _rows;

        if (_state.OpenedTrack != null)
        {
            if (_state.SelectedSegment is { } segment)
            {
                Header("Segment");
                IntRow("Numerator", () => segment.Numerator, v => segment.Numerator = v, 1, 64);
                IntRow("Denominator", () => segment.Denominator, v => segment.Denominator = v, 1, 64);
                IntRow("Bars", () => segment.Bars, v => segment.Bars = v, 1, 1024);
                IntRow("Steps/beat", () => segment.StepsPerBeat, v => segment.StepsPerBeat = v, 1, 64);
                NumberRow("BPM", () => segment.BPM, v => segment.BPM = (float?)v, 1, 9999, allowNull: true);
            }

            if (_state.SelectedNote is { } note)
            {
                Header("Note");
                InfoRow("Instrument", () => note.Instrument.Name);
                ActionRow("Change", () => OnReassignInstrument?.Invoke(note));
                NumberRow("Value", () => note.Value, v => note.Value = v!.Value,
                    -TrackEditorView.MaxValue, TrackEditorView.MaxValue);
                NumberRow("Volume", () => note.Volume, v => note.Volume = v, 0, 500, 5, allowNull: true);
                NumberRow("Pan", () => note.Pan, v => note.Pan = (float)v!.Value, -100, 100, 10);
                NumberRow("Offset (s)", () => note.Offset, v => note.Offset = v!.Value, -60, 60, 0.05);
                AutomationSection(note);
            }
        }
        else
        {
            Header("Project");
            TextRow("Name", () => _state.Project.Info.Name, v => _state.Edit(() => _state.Project.Info.Name = v));
            TextRow("Author", () => _state.Project.Info.Author ?? "",
                v => _state.Edit(() => _state.Project.Info.Author = NullIfEmpty(v)));
            TextRow("Description", () => _state.Project.Info.Description ?? "",
                v => _state.Edit(() => _state.Project.Info.Description = NullIfEmpty(v)));
            NumberRow("BPM", () => _state.Project.RootTiming.BPM,
                v => _state.Project.RootTiming.BPM = (float)v!.Value, 1, 9999);

            if (_state.SelectedTrack is { } track)
            {
                Header("Track");
                TextRow("Name", () => track.Name, v => _state.RenameTrack(track, v));
                CheckRow("Project tempo", () => _state.TrackFollowsRootTiming(track), follows =>
                {
                    _state.SetTrackFollowsRootTiming(track, follows);
                    Rebuild(); // the own-BPM row appears/disappears
                });
                if (!_state.TrackFollowsRootTiming(track))
                    NumberRow("BPM", () => track.Timing.BPM, v => track.Timing.BPM = (float)v!.Value, 1, 9999);

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
        Header("Automation");
        if (note.Automation is not { } automation)
        {
            ActionRow("+ Add automation", () => EditAndRebuild(() => note.Automation = new AudioKeyframeManager()));
            return;
        }

        KeyframeBlocks("Automation", "", automation);
        _section = "Automation";
        ActionRow("Remove automation", () => EditAndRebuild(() => note.Automation = null));
    }

    /// <summary>
    ///     Phase-6-esque form for a whole track: any number of automations, each with its
    ///     own sound filter (null = every sound), instead of one note's single nullable
    ///     <see cref="Note.Automation" />. Shares <see cref="KeyframeBlocks" /> with
    ///     <see cref="AutomationSection" /> for the gap/repeats/keyframe rows.
    /// </summary>
    private void TrackAutomationSection(ProjectTrack track)
    {
        Header("Track Automation");

        for (var i = 0; i < track.TrackAutomations.Count; i++)
        {
            var entry = track.TrackAutomations[i];
            var section = $"Track Automation {i + 1}";

            Card(EntryColor, () =>
            {
                Header(section);

                CheckRow("All sounds", () => entry.Sounds is null,
                    allSounds => EditAndRebuild(() => entry.Sounds = allSounds ? null : []));
                if (entry.Sounds is { } sounds)
                    ActionRow("Sounds", $"Sounds: {sounds.Count} selected",
                        () => OnEditTrackAutomationSounds?.Invoke(entry));

                KeyframeBlocks(section, $"{section} ", entry.Keyframes);

                _section = section;
                ActionRow("Remove", () => EditAndRebuild(() => track.RemoveTrackAutomation(entry)));
            });
        }

        _section = "Track Automation";
        ActionRow("+ Add automation",
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
        _section = section;
        CheckRow("Gaps in seconds", () => automation.Timing == KeyframeTiming.Time,
            timeMode => _state.Edit(() =>
                automation.Timing = timeMode ? KeyframeTiming.Time : KeyframeTiming.Step));
        IntRow("Repeats", () => automation.Repeats, v => automation.Repeats = v, 1, 1024);

        for (var i = 0; i < automation.Keyframes.Count; i++)
        {
            var keyframe = automation.Keyframes[i];
            Card(KeyframeColor, () =>
            {
                Header($"{keyframeHeaderPrefix}Keyframe {i + 1}");
                NumberRow("Gap", () => keyframe.Gap, v => keyframe.Gap = (float)v!.Value, 0, 4096, 0.5);
                CheckRow("Cut", () => keyframe.Cut, cut => _state.Edit(() => keyframe.Cut = cut));
                ModifierRow("Value", () => keyframe.Value, m => keyframe.Value = m);
                ModifierRow("Volume", () => keyframe.Volume, m => keyframe.Volume = m);
                ModifierRow("Pan", () => keyframe.Pan, m => keyframe.Pan = m);
                ModifierRow("Offset", () => keyframe.Offset, m => keyframe.Offset = m);
                ActionRow("Remove", () => EditAndRebuild(() => automation.Keyframes.Remove(keyframe)));
            });
        }

        _section = section;
        ActionRow("+ Keyframe", () => EditAndRebuild(() => automation.Keyframes.Add(new AudioKeyframe())));
    }

    private void EditAndRebuild(Action edit)
    {
        _state.Edit(edit);
        Rebuild();
    }

    /// <summary>
    ///     Boxes one automation/keyframe entry as its own background-filled card so
    ///     adjacent entries in the flat, uniformly-spaced <see cref="_rows" /> list read
    ///     as distinct groups instead of one continuous run of rows.
    /// </summary>
    private void Card(Vector4 color, Action build)
    {
        var card = new FlexPanel(Context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Padding = 8,
            Spacing = 8,
            Background = new ColoredPlane { Color = color, BorderRadius = 4f }
        };
        var previous = _container;
        previous.AddChild(card);
        _container = card;
        build();
        _container = previous;
    }

    /// <summary>
    ///     A relative-change field: amount plus a "×" checkbox choosing multiply over
    ///     add. Committed as one <see cref="Modifier" /> whenever either part changes.
    /// </summary>
    private void ModifierRow(string label, Func<Modifier> get, Action<Modifier> set)
    {
        var initial = get();
        var amount = new NumericInput(Context, initial.Amount)
        {
            Width = 100, Height = 32, FontSizePx = 14, Min = -10000, Max = 10000,
            BorderRadius = 4,
            Background = new ColoredPlane { Color = InputColor }
        };
        var multiply = new Checkbox(Context, "×", initial.Kind == ModifierKind.Multiply)
        {
            X = LabelWidth + 108, Y = 6
        };
        multiply.Label.FontSizePx = 13f;

        void Commit()
        {
            if (amount.Value is not { } value) return; // mid-edit ("", "-")
            _state.Edit(() =>
                set(new Modifier(value, multiply.Checked ? ModifierKind.Multiply : ModifierKind.Add)));
        }

        amount.OnValueChanged = _ => Commit();
        multiply.OnCheckedChanged = _ => Commit();

        _fields[$"{_section}.{label}.Kind"] = multiply;
        Row(label, amount, multiply);
        _syncs.Add(() =>
        {
            if (!amount.IsFocused) amount.Value = get().Amount;
            multiply.Checked = get().Kind == ModifierKind.Multiply;
        });
    }

    private void ActionRow(string label, Action onClick)
    {
        ActionRow(label, label, onClick);
    }

    /// <summary>A key/label split for buttons whose display text is dynamic (e.g. a
    /// selection count) but whose field key must stay stable.</summary>
    private void ActionRow(string key, string label, Action onClick)
    {
        var button = new Button(Context, label) { FontSizePx = 13f, OnClick = _ => onClick() };
        _fields[$"{_section}.{key}"] = button;
        _container.AddChild(button);
    }

    /// <summary>Writes the model values into the rows. Call on any model change.</summary>
    public void Sync()
    {
        // Snapshot: a sync-triggered handler may Rebuild (and clear) the list.
        foreach (var sync in _syncs.ToArray()) sync();
    }

    private void Header(string text)
    {
        _section = text;
        _container.AddChild(new Label(Context, text) { FontSizePx = 15f, Color = HeaderColor });
    }

    private void Row(string label, UIElement input, UIElement? extra = null)
    {
        _fields[$"{_section}.{label}"] = input;
        input.X = LabelWidth;
        var row = new Panel(Context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = RowHeight
        };
        row.AddChild(new Label(Context, label) { FontSizePx = 13f, Color = NameColor, Y = 9 });
        row.AddChild(input);
        if (extra != null) row.AddChild(extra);
        _container.AddChild(row);
    }

    private void TextRow(string label, Func<string> get, Action<string> set)
    {
        var input = new TextInput(Context, get())
        {
            Width = FieldWidth,
            FontSizePx = 14f,
            BorderRadius = 4,
            Background = new ColoredPlane { Color = InputColor },
            OnValueChanged = i => set(i.Value)
        };
        Row(label, input);
        _syncs.Add(() =>
        {
            if (!input.IsFocused) input.Value = get();
        });
    }

    /// <summary>
    ///     A numeric field routed through <see cref="EditorState.Edit" />. Non-nullable
    ///     fields ignore the transient null while the text is mid-edit ("", "-");
    ///     nullable ones commit it (empty = inherit).
    /// </summary>
    private void NumberRow(string label, Func<double?> get, Action<double?> set,
        double min, double max, double step = 1, bool allowNull = false)
    {
        var input = new NumericInput(Context, get())
        {
            Width = FieldWidth,
            Height = 32,
            FontSizePx = 14f,
            Min = min,
            Max = max,
            Step = step,
            AllowNull = allowNull,
            BorderRadius = 4,
            Background = new ColoredPlane { Color = InputColor }
        };
        input.OnValueChanged = _ =>
        {
            var value = input.Value;
            if (value != null || allowNull) _state.Edit(() => set(value));
        };
        Row(label, input);
        _syncs.Add(() =>
        {
            if (!input.IsFocused) input.Value = get();
        });
    }

    private void IntRow(string label, Func<int> get, Action<int> set, int min, int max)
    {
        NumberRow(label, () => get(), v => set((int)Math.Round(v!.Value)), min, max);
    }

    private void CheckRow(string label, Func<bool> get, Action<bool> set)
    {
        var box = new Checkbox(Context, label, get()) { OnCheckedChanged = b => set(b.Checked) };
        box.Label.FontSizePx = 13f;
        _fields[$"{_section}.{label}"] = box;
        _container.AddChild(box);
        _syncs.Add(() => box.Checked = get());
    }

    private void InfoRow(string label, Func<string> get)
    {
        var value = new Label(Context, get()) { FontSizePx = 13f, Y = 9 };
        Row(label, value);
        _syncs.Add(() => value.SetTextContents(get()));
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

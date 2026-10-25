using System.Diagnostics;
using System.Globalization;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Shared.Renderer.Planes.Uniforms;
using Sundex.Core;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Markup;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Components;
using EditorScene.Scenes.Views;
using VisualizerScene.Objects.Playfield;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The form for an action that carries a value, opened from the palette (a new item, added
///     at the end) or by right-clicking the slot (an edit). Its parts, top to bottom: the slot
///     drawn between the track's own slots around it (<see cref="Window" />); a list of what the
///     value can be - the ways it applies (S/A/M/D), common values, the track's targets, colors;
///     the fields with what the slot means where it lands; and a "!bg"/"!pulse" preview box.
///     The wording and the bounds are the site's popups'. Pure form - the owner adds or swaps in
///     <see cref="Result" /> and closes the modal; <see cref="Release" /> frees the strip once it
///     is closed. The tree is ActionValueDialog.snx.xml; the list, the card and the strip are here.
/// </summary>
public sealed class ActionValueDialog
{
    /// <summary>Slots the strip shows. Odd, so a centred slot has as many neighbours on each side.</summary>
    public const int StripSlots = 9;

    private const float TileSize = 52;
    private const float TileMargin = 10;

    /// <summary>A fade preview never runs longer than this; the site's fades go to 128 s.</summary>
    private const double MaxPreviewFadeSeconds = 4;

    /// <summary>Target rows that fit the default body beside the card.</summary>
    private const int MaxTargetRows = 4;

    /// <summary>A pulse preview stops after this many, however many the slot asks for.</summary>
    private const int MaxPreviewPulses = 16;

    /// <summary>The site's own background (its <c>defaultBG</c>): what a fade starts from with no earlier "!bg", and what a pulse plays over.</summary>
    private static readonly Vector4 SiteBackground = Rgb("36393C");

    private static readonly ValueScale[] AllModes = [ValueScale.None, ValueScale.Add, ValueScale.Times, ValueScale.Divide];

    private static readonly (string Name, string Hex)[] Colors =
    [
        ("Site default", "36393C"), ("Black", "000000"), ("Blue", "4C6BCC"), ("Purple", "9E5CB5"),
        ("Green", "3D9975"), ("Orange", "C77840"), ("Rose", "B8526B"), ("Teal", "478FAB"),
        ("Olive", "A89447"), ("Amber", "E0AF68")
    ];

    /// <summary>
    ///     What the form shows for each action: the site popup's title and text, the field, and
    ///     the ways the value can apply (the site's Set to / Add / Multiply; Alt+Multiply divides).
    ///     The body is tall enough for the list beside the card - ten colors, two fields and a
    ///     preview, five common values, or four modes.
    /// </summary>
    private static readonly Dictionary<string, Form> Forms = new()
    {
        ["!speed"] = new Form("Set tempo", "Sets the rate sounds play at. Default is 300 BPM.", "Tempo", "BPM", AllModes),
        ["!volume"] = new Form("Set volume", "Sets the volume of every sound after it. Default is 100%, max is 600%.",
            "Volume", "%", AllModes),
        ["!stop"] = new Form("Pause", "Waits the given number of beats before the next sound.", "Length", "beats",
            Presets: [1, 2, 4, 8, 16]),
        ["!transpose"] = new Form("Transpose", "Raises or lowers the pitch of every sound after it.", "Pitch",
            "semitones", [ValueScale.None, ValueScale.Add]),
        ["!loopmany"] = new Form("Loop", "Goes back to the loop start this many times, or to the track's start.",
            "Repeats", "times", Presets: [1, 2, 3, 4, 8]),
        ["!jump"] = new Form("Go to target", "Jumps to the target with this number, once.", "Target"),
        ["!target"] = new Form("Target", "A numbered landing point. Go to target jumps here.", "Number"),
        ["!bg"] = new Form("Background color", "Fades the background to a color.", BodyHeight: 350),
        ["!pulse"] = new Form("Pulse", "Pulses the screen a number of times, ignoring tempo changes.", BodyHeight: 350)
    };

    private readonly FaithfulAction _action;
    private readonly Panel? _colorChip;
    private readonly bool _editing;
    private readonly ValueField? _every;
    private readonly ValueField? _fade;
    private readonly FlexPanel _facts;
    private readonly Form _form;
    private readonly TextInput? _hex;
    private readonly PreviewBox? _preview;
    private readonly ValueField? _pulses;
    private readonly List<Row> _rows = [];

    /// <summary>Where the slot sits in <see cref="_stream" /> - one past its end for a new item.</summary>
    private readonly int _slot;

    /// <summary>The track's slots as the sequence draws them, the edited one included.</summary>
    private readonly BaseEvent[] _stream;

    private readonly EventCanvas? _strip;
    private readonly Label _tdw;
    private readonly ValueField? _value;

    /// <summary>What the strip showed last; kept while the fields hold no event.</summary>
    private BaseEvent _shown;

    /// <summary>The slot's index in the strip's window, for its lit panel.</summary>
    private int _shownAt;

    private string[] _factTexts = [];

    /// <summary>The "!bg" preview's start color, fade time and end color, under it; null for other actions.</summary>
    private readonly Label[]? _scale;

    /// <param name="context">The editor's UI context.</param>
    /// <param name="action">The palette entry the slot is.</param>
    /// <param name="track">The open faithful track, for the strip and what the slot means where it lands.</param>
    /// <param name="index">The edited item's index in <paramref name="track" />, or its item count for a new one.</param>
    /// <param name="current">The edited slot's event, or null for a new item, which starts from the site's default.</param>
    /// <param name="look">The strip's playfield look. Null leaves the strip out - the headless tests have no atlases.</param>
    public ActionValueDialog(UIContext context, FaithfulAction action, FaithfulTrack track, int index,
        BaseEvent? current = null, PlayfieldSettings? look = null)
    {
        _action = action;
        _form = Forms.GetValueOrDefault(action.Name) ?? new Form(action.Name, action.Hint, "Value");
        _editing = current is not null;

        var component = Markup.Build(context, "Scenes/Dialogs/Action Value Dialog/ActionValueDialog.snx.xml");
        Element = component.GetID<FlexPanel>("action-value-dialog");
        CloseButton = component.GetID<Button>("close-button");
        CancelButton = component.GetID<Button>("cancel-button");
        SaveButton = component.GetID<Button>("save-button");
        _tdw = component.GetID<Label>("tdw-label");
        component.GetID<Label>("title-label").SetTextContents(_form.Title);
        component.GetID<Label>("token-label").SetTextContents(action.Name);
        component.GetID<Label>("description-label").SetTextContents(_form.Description);
        component.GetID<Label>("save-label").SetTextContents(_editing ? "Save" : "Add");
        KeyChip.Add(CancelButton, "Esc");
        KeyChip.Add(SaveButton, "Enter");

        // The slots as the sequence draws them, once: the fields only ever change the one slot.
        var tagged = track.ExpandTagged().ToArray();
        _stream = [.. tagged.Select(pair => pair.Event)];
        var item = _editing && index >= 0 && index < track.Items.Count ? track.Items[index] : null;
        _slot = item is null ? _stream.Length : Math.Max(0, Array.FindIndex(tagged, pair => pair.Item == item));
        _shown = current?.Copy() ?? action.PaletteEvent();

        var body = component.GetID<FlexPanel>("action-body");
        body.Height = _form.BodyHeight;
        var card = component.GetID<FlexPanel>("action-card");
        _facts = new FlexPanel(context) { Classes = ["track-kind-facts"] };

        switch (action.Name)
        {
            case "!bg":
            {
                var (hex, fade) = current is null ? ("FF0000", 0.5) : UnpackBackground(current.Value);
                _hex = new TextInput(context, hex) { Classes = ["action-field-input"] };
                _colorChip = new Panel(context) { Classes = ["action-color-chip"], Background = new ColoredPlane() };
                _fade = Number(context, fade, 0, 128, 0.1, "s");
                card.AddChild(Field(context, "Color",
                    new FlexPanel(context) { Classes = ["action-color-row"], Children = [_colorChip, _hex] }));
                card.AddChild(Field(context, "Fade time", _fade));
                _preview = new PreviewBox(context);
                break;
            }
            case "!pulse":
            {
                var (pulses, every) = NormalEvent.UnpackPulse(current?.Value ?? PulseTemplate());
                _pulses = Number(context, pulses, 0, 1000, 1, "in total");
                _every = Number(context, every, 1, 128, 1, "beats");
                card.AddChild(Field(context, "Pulses", _pulses));
                card.AddChild(Field(context, "Every", _every));
                _preview = new PreviewBox(context);
                break;
            }
            default:
            {
                _value = new ValueField(context, this) { Classes = ["action-field-input"], Unit = _form.Unit };
                Mode = current is not null && Modes.Contains(current.ValueScale) ? current.ValueScale : ValueScale.None;
                ApplyBounds();
                _value.Value = current?.Value ?? StartValue();
                card.AddChild(Field(context, _form.Label, _value));
                break;
            }
        }

        if (_preview is not null)
        {
            card.AddChild(new Label(context, "Preview") { Classes = ["action-preview-label"] });
            card.AddChild(_preview);
            if (_hex is not null)
            {
                // Under the fade at rest: where it starts, how long it takes, where it ends.
                _scale = [new Label(context, "") { Classes = ["action-preview-scale-label"] },
                    new Label(context, "") { Classes = ["action-preview-scale-label"] },
                    new Label(context, "") { Classes = ["action-preview-scale-label"] }];
                card.AddChild(new FlexPanel(context)
                {
                    Classes = ["action-preview-scale"],
                    Children =
                    [
                        _scale[0], new Panel(context) { Classes = ["action-preview-scale-gap"] }, _scale[1],
                        new Panel(context) { Classes = ["action-preview-scale-gap"] }, _scale[2]
                    ]
                });
            }
        }
        // Without a preview, the facts still sit at the bottom of the card.
        else card.AddChild(new Panel(context) { Classes = ["action-fill"] });

        card.AddChild(_facts);
        if (_preview is not null)
        {
            var play = new Button(context, _action.Name == "!bg" ? "Play the fade" : "Play the pulses")
                { Classes = ["action-play-button"] };
            play.Children = [new Label(context, Icons.Play, Icons.Font) { Classes = ["action-play-icon"] }, play.Label];
            play.OnClick = _ => Play();
            card.AddChild(play);
        }

        BuildList(context, component);

        foreach (var field in Fields) field.OnValueChanged = _ => Refresh();

        if (look is not null)
        {
            _strip = new EventCanvas(context, look, TileSize, TileMargin, StripSlots)
            {
                Width = LiteralOrComputable.Percent(100),
                BreakOnDividers = false,
                HighlightHover = false,
                IsSelected = i => i == _shownAt
            };
            component.GetID<FlexPanel>("action-strip").AddChild(_strip);
        }
        else component.GetID<FlexPanel>("action-strip").Visible = false;

        Refresh();
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public Button CloseButton { get; }
    public Button CancelButton { get; }

    /// <summary>"Add" for a new item, "Save" for an edit.</summary>
    public Button SaveButton { get; }

    /// <summary>How the value applies: set (None), add, multiply (Times) or divide.</summary>
    public ValueScale Mode { get; private set; }

    /// <summary>The ways this action's value can apply, Set first; one entry when it only sets.</summary>
    private IReadOnlyList<ValueScale> Modes => _form.Modes ?? [ValueScale.None];

    /// <summary>The main field: the value, the color, or the pulse count.</summary>
    public TextInput MainField => (TextInput?)_value ?? (TextInput?)_hex ?? _pulses!;

    /// <summary>The slot as TDW text - what <see cref="Result" /> parses and the footer shows.</summary>
    public string Tdw => _action.Name switch
    {
        "!bg" => $"!bg@#{HexText},{Format(_fade!.Value ?? 0)}",
        "!pulse" => $"!pulse@{Format(Math.Round(_pulses!.Value ?? 0))},{Format(Math.Round(_every!.Value ?? 1))}",
        _ => $"{_action.Name}@{Format(_value!.Value ?? 0)}{Suffix(Mode)}"
    };

    /// <summary>The item the form describes, or null while a field holds nothing it can write.</summary>
    public FaithfulItem? Result => Valid ? FaithfulItem.Parse(Tdw) : null;

    /// <summary>What the slot means where it lands, in the card's words; a warning is flagged.</summary>
    public IReadOnlyList<(string Text, bool Warn)> Facts => BuildFacts();

    /// <summary>The window the strip shows, for tests: its first slot in the stream, and the slot's place in it.</summary>
    public (int Start, int Slot) StripWindow
    {
        get
        {
            var (start, _) = Window(Total, _slot, StripSlots);
            return (start, _slot - start);
        }
    }

    private int Total => _editing ? _stream.Length : _stream.Length + 1;

    private IEnumerable<TextInput> Fields =>
        new TextInput?[] { _value, _hex, _fade, _pulses, _every }.OfType<TextInput>();

    private string HexText => (_hex?.Value ?? "").Trim().TrimStart('#').ToUpperInvariant();

    private bool HexValid => HexText.Length is 6 or 8 && HexText.All(Uri.IsHexDigit);

    private bool Valid => _action.Name switch
    {
        "!bg" => HexValid && _fade!.Value is not null,
        "!pulse" => _pulses!.Value is not null && _every!.Value is not null,
        _ => _value!.Value is not null
    };

    /// <summary>
    ///     The strip's slots: <paramref name="capacity" /> of them around <paramref name="slot" />,
    ///     with the slot centred when the stream has enough on both sides of it. Otherwise the
    ///     window stops at the stream's end, and the slot sits where it falls, left to right - a
    ///     new item, always the last, sits at the right end.
    /// </summary>
    /// <param name="total">Slots in the stream, the one being added included.</param>
    /// <param name="slot">The slot's index in the stream.</param>
    /// <param name="capacity">Slots the strip has room for.</param>
    public static (int Start, int Count) Window(int total, int slot, int capacity)
    {
        if (total <= capacity) return (0, total);
        return (Math.Clamp(slot - capacity / 2, 0, total - capacity), capacity);
    }

    /// <summary>One "!speed"/"!volume"/"!transpose" applied to the running value, as the walker does.</summary>
    private static double Apply(double current, ValueScale scale, double value)
    {
        return scale switch
        {
            ValueScale.Divide => current / value,
            ValueScale.Times => current * value,
            ValueScale.Add => current + value,
            _ => value
        };
    }

    /// <summary>Picks how the value applies. False when this action has no such mode.</summary>
    public bool Pick(ValueScale mode)
    {
        if (_value is null || !Modes.Contains(mode)) return false;
        Mode = mode;
        ApplyBounds();
        Refresh();
        return true;
    }

    /// <summary>S/A/M/D pick how the value applies, Up/Down step the main field - for when no field has focus.</summary>
    /// <returns>True for a key the dialog used.</returns>
    public bool HandleKey(KeyboardKeyEventArgs e)
    {
        if (e.Control || e.Alt || e.Command) return false;
        if (e.Key is Keys.Up or Keys.Down && MainField is NumericInput number)
        {
            number.StepBy(e.Key == Keys.Up ? 1 : -1);
            return true;
        }

        return ModeKey(e.Key) is { } mode && Pick(mode);
    }

    /// <summary>Puts the caret in the main field, its text selected so typing replaces it. Call once the modal is mounted.</summary>
    public void Focus()
    {
        MainField.Context.Focus(MainField);
        MainField.SelectAll();
    }

    /// <summary>Frees the strip's GL buffers - nothing else owns them. Call once the modal is closed.</summary>
    public void Release()
    {
        _strip?.SetEvents([]);
    }

    private static ValueScale? ModeKey(Keys key)
    {
        return key switch
        {
            Keys.S => ValueScale.None,
            Keys.A => ValueScale.Add,
            Keys.M => ValueScale.Times,
            Keys.D => ValueScale.Divide,
            _ => null
        };
    }

    private static string ModeName(ValueScale mode)
    {
        return mode switch
        {
            ValueScale.Add => "Add",
            ValueScale.Times => "Multiply",
            ValueScale.Divide => "Divide",
            _ => "Set to"
        };
    }

    private static string ModeKeyName(ValueScale mode)
    {
        return mode switch
        {
            ValueScale.Add => "A",
            ValueScale.Times => "M",
            ValueScale.Divide => "D",
            _ => "S"
        };
    }

    private static string Suffix(ValueScale mode)
    {
        return mode switch
        {
            ValueScale.Add => "@+",
            ValueScale.Times => "@x",
            ValueScale.Divide => "@/",
            _ => ""
        };
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Plural(double n, string one, string many)
    {
        return Math.Abs(n - 1) < 1e-9 ? one : many;
    }

    private static Vector4 Rgb(string hex)
    {
        return new Vector4(Convert.ToByte(hex[..2], 16) / 255f, Convert.ToByte(hex[2..4], 16) / 255f,
            Convert.ToByte(hex[4..6], 16) / 255f, 1f);
    }

    private static string Hex(Vector4 color)
    {
        return $"{(int)MathF.Round(color.X * 255):X2}{(int)MathF.Round(color.Y * 255):X2}{(int)MathF.Round(color.Z * 255):X2}";
    }

    /// <summary>A "!bg" value's color (alpha kept when it isn't opaque) and fade, as the parser packed them.</summary>
    private static (string Hex, double Fade) UnpackBackground(double packed)
    {
        var v = (long)packed;
        var alpha = (byte)(v >> 24);
        var hex = $"{(byte)v:X2}{(byte)(v >> 8):X2}{(byte)(v >> 16):X2}" + (alpha == 255 ? "" : $"{alpha:X2}");
        return (hex, (v >> 32) / 1000.0);
    }

    private static Vector4 BackgroundColor(double packed)
    {
        var v = (long)packed;
        return new Vector4((byte)v / 255f, (byte)(v >> 8) / 255f, (byte)(v >> 16) / 255f, 1f);
    }

    private double PulseTemplate()
    {
        return _action.Template is { } template && FaithfulItem.Parse(template)?.Action is { } ev ? ev.Value : 1 << 8 | 2;
    }

    private ValueField Number(UIContext context, double value, double min, double max, double step, string unit)
    {
        // Value last: it clamps to the bounds set before it.
        return new ValueField(context, this)
        {
            Classes = ["action-field-input"], Min = min, Max = max, Step = step, ScrollStep = step, Unit = unit,
            Value = value
        };
    }

    private static FlexPanel Field(UIContext context, string label, UIElement input)
    {
        return new FlexPanel(context)
        {
            Classes = ["action-field"],
            Children = [new Label(context, label) { Classes = ["field-label"] }, input]
        };
    }

    /// <summary>The field's bounds for the current mode - the site's, which the scroll gesture shares.</summary>
    private void ApplyBounds()
    {
        var range = FaithfulAction.ScrollRangeFor(new NormalEvent { SoundEvent = _action.Name, ValueScale = Mode })
                    ?? new ScrollRange(double.MinValue, double.MaxValue);
        _value!.Min = range.Min;
        _value.Max = range.Max;
        _value.Step = range.Step;
        _value.ScrollStep = range.Step;
    }

    /// <summary>
    ///     A new slot's value: the palette's default, except that a target takes the first number
    ///     no target has, and a jump the first target nothing jumps to yet - the site numbers
    ///     them for you too.
    /// </summary>
    private double StartValue()
    {
        var targets = Numbers("!target");
        var jumps = Numbers("!jump");
        return _action.Name switch
        {
            "!target" => Enumerable.Range(1, targets.Count + 1).First(n => !targets.Contains(n)),
            "!jump" => targets.FirstOrDefault(n => !jumps.Contains(n), targets.FirstOrDefault(1)),
            _ => _action.PaletteEvent().Value
        };
    }

    /// <summary>The numbers the stream's events of one name carry, in stream order, the edited slot left out.</summary>
    private List<int> Numbers(string name)
    {
        return [.. Indices(name).Select(i => (int)Math.Round(_stream[i].Value)).Distinct()];
    }

    private IEnumerable<int> Indices(string name)
    {
        for (var i = 0; i < _stream.Length; i++)
            if (i != _slot && _stream[i].SoundEvent == name)
                yield return i;
    }

    /// <summary>The value an event of this name leaves behind just before the slot, read in stream order.</summary>
    private double Before(string name, double start)
    {
        var value = start;
        for (var i = 0; i < _slot && i < _stream.Length; i++)
            if (_stream[i].SoundEvent == name)
                value = Apply(value, _stream[i].ValueScale, _stream[i].Value);
        return value;
    }

    private Vector4 PreviousBackground()
    {
        for (var i = Math.Min(_slot, _stream.Length) - 1; i >= 0; i--)
            if (_stream[i].SoundEvent == "!bg")
                return BackgroundColor(_stream[i].Value);
        return SiteBackground;
    }

    private string Distance(int index)
    {
        var d = index - _slot;
        return d < 0 ? $"{-d} {Plural(-d, "slot", "slots")} back" : $"{d} {Plural(d, "slot", "slots")} ahead";
    }

    private string Show(double value)
    {
        return _action.Name switch
        {
            "!speed" => $"{Format(value)} BPM",
            "!volume" => $"{Format(value)}%",
            _ => (value > 0 ? "+" : "") + Format(value)
        };
    }

    private double Start => _action.Name switch { "!speed" => 300, "!volume" => 100, _ => 0 };

    private string BeforeLabel => _action.Name switch
    {
        "!speed" => "Tempo before",
        "!volume" => "Volume before",
        _ => "Transpose before"
    };

    private List<(string Text, bool Warn)> BuildFacts()
    {
        var facts = new List<(string, bool)>();
        var tempo = Before("!speed", 300);

        if (_value is not null)
        {
            if (_value.Value is not { } v)
            {
                facts.Add(("Type a number", true));
                return facts;
            }

            if (double.TryParse(_value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed) &&
                (typed < _value.Min || typed > _value.Max))
                facts.Add(($"Kept within {Format(_value.Min)} – {Format(_value.Max)}", true));

            switch (_action.Name)
            {
                case "!speed" or "!volume" or "!transpose":
                {
                    var before = Before(_action.Name, Start);
                    facts.Add(($"{BeforeLabel}: {Show(before)}", false));
                    facts.Add(($"After this: {Show(Apply(before, Mode, v))}", false));
                    break;
                }
                case "!stop":
                    facts.Add(($"Lasts {v * 60 / tempo:0.00} s at {Format(tempo)} BPM", false));
                    break;
                case "!loopmany":
                {
                    var loopStart = Indices("!looptarget").Where(i => i < _slot).DefaultIfEmpty(-1).Last();
                    facts.Add((loopStart < 0
                        ? "Goes back to the start of the track"
                        : $"Goes back to the loop start, {Distance(loopStart)}", false));
                    facts.Add(($"That part plays {Format(v + 1)} times in all", false));
                    break;
                }
                case "!jump":
                {
                    // The walker lands on the first target with the number, wherever it is.
                    var target = Indices("!target").Where(i => Math.Abs(_stream[i].Value - v) < 0.001)
                        .DefaultIfEmpty(-1).First();
                    facts.Add(target < 0
                        ? ($"No target {Format(v)} in this track yet", true)
                        : ($"Lands on target {Format(v)}, {Distance(target)}", false));
                    break;
                }
                case "!target":
                {
                    var twin = Indices("!target").Where(i => Math.Abs(_stream[i].Value - v) < 0.001)
                        .DefaultIfEmpty(-1).First();
                    if (twin >= 0) facts.Add(($"Target {Format(v)} is already in this track, {Distance(twin)}", true));
                    var jumps = Indices("!jump").Count(i => Math.Abs(_stream[i].Value - v) < 0.001);
                    facts.Add((jumps == 0
                        ? $"No jump goes to {Format(v)} yet"
                        : $"{jumps} {Plural(jumps, "jump lands", "jumps land")} here", false));
                    break;
                }
            }

            return facts;
        }

        if (_hex is not null)
        {
            if (!HexValid) facts.Add(("Type 6 hex digits, like 36393C", true));
            var from = "#" + Hex(PreviousBackground());
            var fade = _fade!.Value ?? 0;
            facts.Add((fade > 0 ? $"Fades from {from} over {Format(fade)} s" : $"Switches from {from} at once", false));
            return facts;
        }

        var pulses = Math.Round(_pulses!.Value ?? 0);
        var every = Math.Round(_every!.Value ?? 1);
        facts.Add(($"{Format(pulses)} {Plural(pulses, "pulse", "pulses")}, one every {Format(every)} {Plural(every, "beat", "beats")}", false));
        facts.Add(($"Lasts {Format(pulses * every)} beats", false));
        facts.Add(("Only one pulse runs at a time", false));
        return facts;
    }

    /// <summary>
    ///     The list beside the card: the ways the value applies, common values, the track's
    ///     targets, or colors. An action with nothing to list ("!pulse", a jump in a track with
    ///     no targets) gives the card the whole width. No scroller: every list fits its body,
    ///     the targets by keeping the ones nearest the slot.
    /// </summary>
    private void BuildList(UIContext context, SundexComponent component)
    {
        var column = component.GetID<FlexPanel>("action-list-column");
        var label = component.GetID<Label>("action-list-label");
        var list = component.GetID<FlexPanel>("action-list");

        if (Modes.Count > 1)
        {
            label.SetTextContents("Applies as");
            foreach (var mode in Modes)
                AddRow(context, list, ModeName(mode), () => Pick(mode), () => Mode == mode,
                    () => _value!.Value is { } v ? "→ " + Show(Apply(Before(_action.Name, Start), mode, Clamp(mode, v))) : "",
                    chip: ModeKeyName(mode));
        }
        else if (_form.Presets is { } presets)
        {
            label.SetTextContents(_action.Name == "!stop" ? "Common lengths" : "Common counts");
            foreach (var n in presets)
                AddRow(context, list,
                    _action.Name == "!stop" ? $"{n} {Plural(n, "beat", "beats")}" : $"{n} {Plural(n, "time", "times")}",
                    () => _value!.Value = n, () => Holds(n),
                    () => _action.Name == "!stop"
                        ? $"{n * 60 / Before("!speed", 300):0.00} s"
                        : $"plays {Format(n + 1)}×");
        }
        else if (_action.Name is "!jump" or "!target")
        {
            label.SetTextContents(_action.Name == "!jump" ? "Targets in this track" : "Numbers in this track");
            var room = _action.Name == "!jump" ? MaxTargetRows : MaxTargetRows - 1; // a target's list ends on the free number
            foreach (var i in Indices("!target").DistinctBy(i => Math.Round(_stream[i].Value))
                         .OrderBy(i => Math.Abs(i - _slot)).Take(room).Order())
            {
                var n = Math.Round(_stream[i].Value);
                var where = Distance(i);
                AddRow(context, list, $"Target {Format(n)}", () => _value!.Value = n, () => Holds(n),
                    () => where);
            }

            if (_action.Name == "!target")
            {
                var taken = Numbers("!target");
                var free = Enumerable.Range(1, taken.Count + 1).First(n => !taken.Contains(n));
                AddRow(context, list, $"Target {free}", () => _value!.Value = free, () => Holds(free),
                    () => _editing && Math.Abs(_shown.Value - free) < 0.001 ? "this slot" : "free");
            }
        }
        else if (_pulses is not null)
        {
            label.SetTextContents("Common counts");
            foreach (var n in (double[])[1, 2, 4, 8])
                AddRow(context, list, $"{n} {Plural(n, "pulse", "pulses")}", () => _pulses.Value = n,
                    () => _pulses.Value is { } v && Math.Abs(v - n) < 1e-9,
                    () => $"{Format(n * Math.Round(_every!.Value ?? 1))} beats");
        }
        else if (_hex is not null)
        {
            label.SetTextContents("Colors");
            foreach (var (name, hex) in Colors)
                AddRow(context, list, name, () => _hex.Value = hex, () => HexText.StartsWith(hex, StringComparison.Ordinal) && HexValid,
                    () => "#" + hex, swatch: hex);
        }

        // A jump with nowhere to go yet keeps its column, saying so, rather than dropping it.
        if (_rows.Count == 0 && _action.Name == "!jump")
            column.AddChild(EmptyState.Build(context, Icons.Target, "No targets in this track yet."));
        else if (_rows.Count == 0 && column.Parent is Panel body) body.RemoveChild(column);
    }

    /// <summary>Whether the value field holds <paramref name="n" /> - a list row's lit state.</summary>
    private bool Holds(double n)
    {
        return _value?.Value is { } v && Math.Abs(v - n) < 1e-9;
    }

    private double Clamp(ValueScale mode, double value)
    {
        return FaithfulAction.ScrollRangeFor(new NormalEvent { SoundEvent = _action.Name, ValueScale = mode }) is { } r
            ? Math.Clamp(value, r.Min, r.Max)
            : value;
    }

    private void AddRow(UIContext context, FlexPanel list, string name, Action pick, Func<bool> on, Func<string> sub,
        string? chip = null, string? swatch = null)
    {
        var row = new FlexPanel(context)
        {
            Classes = swatch is null ? ["action-row", "track-kind-row-idle"] : ["action-row", "action-row-short", "track-kind-row-idle"],
            UpdateCursorOnHover = true,
            OnClick = _ => pick()
        };
        if (chip is not null)
            row.AddChild(new FlexPanel(context)
            {
                Classes = ["key-chip", "key-chip-on-card"],
                Children = [new Label(context, chip) { Classes = ["key-chip-label", "key-chip-label-on-quiet"] }]
            });
        if (swatch is not null)
            row.AddChild(new Panel(context)
                { Classes = ["action-row-swatch"], Background = new ColoredPlane { Color = Rgb(swatch) } });
        row.AddChild(new Label(context, name) { Classes = ["action-row-name"] });
        row.AddChild(new Panel(context) { Classes = ["spacer"] });
        var subLabel = new Label(context, "") { Classes = ["action-row-sub"] };
        row.AddChild(subLabel);
        list.AddChild(row);
        _rows.Add(new Row(row, subLabel, on, sub));
    }

    /// <summary>Brings every part in line with the fields: the rows, the facts, the TDW text, the strip and the preview.</summary>
    private void Refresh()
    {
        foreach (var row in _rows)
        {
            var selected = row.On();
            // Swapped rather than modified: a hover override comes from the first class with one.
            row.Panel.SetClass("track-kind-row-idle", !selected);
            row.Panel.SetClass("track-kind-row-selected", selected);
            row.Sub.SetTextContents(row.Text());
        }

        var facts = BuildFacts();
        var texts = facts.Select(f => (f.Warn ? "!" : "") + f.Text).ToArray();
        if (!texts.SequenceEqual(_factTexts))
        {
            _factTexts = texts;
            foreach (var child in _facts.Children.ToArray()) _facts.RemoveChild(child);
            foreach (var (text, warn) in facts)
                _facts.AddChild(new FlexPanel(_facts.Context)
                {
                    Classes = ["track-kind-fact"],
                    Children =
                    [
                        new Panel(_facts.Context) { Classes = ["track-kind-fact-dot"] },
                        new Label(_facts.Context, text)
                            { Classes = warn ? ["track-kind-fact-label", "track-kind-fact-warn"] : ["track-kind-fact-label"] }
                    ]
                });
        }

        _tdw.SetTextContents(Valid ? Tdw : "");
        if (Result?.Action is { } ev) _shown = ev;

        if (_colorChip?.Background is ColoredPlane chip) chip.Color = HexValid ? Rgb(HexText) : PreviousBackground();
        if (_preview is not null)
        {
            _preview.From = _hex is not null ? PreviousBackground() : SiteBackground;
            _preview.To = _hex is not null && HexValid ? Rgb(HexText) : _preview.From;
            _preview.Ramp = _hex is not null;
            _preview.Ticks = _pulses?.Value is { } pulses ? (int)Math.Round(pulses) : 0;
            _preview.Rest();
        }

        if (_scale is not null)
        {
            _scale[0].SetTextContents("#" + Hex(_preview!.From));
            _scale[1].SetTextContents(_fade!.Value is { } fade ? $"{Format(fade)} s" : "");
            _scale[2].SetTextContents(HexValid ? "#" + HexText[..6] : "");
        }

        if (_strip is null) return;
        var (start, count) = Window(Total, _slot, StripSlots);
        _shownAt = _slot - start;
        _strip.SetEvents(Enumerable.Range(start, count).Select(at => at == _slot ? _shown : _stream[at]).ToArray());
        _strip.RefreshSelection();
    }

    /// <summary>Plays the fade from the previous background, or the pulses at the tempo where the slot lands.</summary>
    private void Play()
    {
        if (_preview is null) return;
        if (_hex is not null)
        {
            _preview.PlayFade(Math.Min(MaxPreviewFadeSeconds, _fade!.Value ?? 0));
            return;
        }

        var gap = Math.Round(_every!.Value ?? 1) * 60 / Before("!speed", 300);
        _preview.PlayPulses((int)Math.Min(MaxPreviewPulses, Math.Round(_pulses!.Value ?? 0)), gap);
    }

    private sealed record Form(
        string Title,
        string Description,
        string Label = "",
        string Unit = "",
        ValueScale[]? Modes = null,
        double[]? Presets = null,
        // Two heights only - every value action's, and the preview actions' - so moving between
        // actions doesn't resize the dialog each time.
        float BodyHeight = 216);

    private sealed record Row(FlexPanel Panel, Label Sub, Func<bool> On, Func<string> Text);

    /// <summary>
    ///     A number field. A focused field owns plain letters, so the mode keys are taken here
    ///     before they reach it - the digits a value is typed with are the only keys it needs.
    /// </summary>
    private sealed class ValueField(UIContext context, ActionValueDialog dialog) : NumericInput(context)
    {
        public override bool HandleKeyDown(KeyboardKeyEventArgs e)
        {
            if (e is { Control: false, Alt: false, Command: false } && ModeKey(e.Key) is { } mode && dialog.Pick(mode))
                return true;
            return base.HandleKeyDown(e);
        }
    }

    /// <summary>
    ///     The "!bg"/"!pulse" preview: a box of background that fades from the previous "!bg"'s
    ///     color, or pulses the way the site pulses the page (its <c>screenpulse</c>: up to 1.05
    ///     at 40% of 0.33 s, then back). Driven per frame from a Stopwatch; the fill's rectangle
    ///     is moved directly, so a pulse never relayouts the card.
    /// </summary>
    private sealed class PreviewBox : Panel
    {
        private const double PulseSeconds = 0.33;
        private const double PulsePeak = 0.4;
        private const float PulseScale = 1.05f;

        /// <summary>Ticks drawn at most; a longer run of pulses fills the strip with these.</summary>
        private const int MaxTicks = 32;

        private readonly Stopwatch _clock = new();
        private readonly ColoredPlane _fill = new();
        private readonly GradientPlane _ramp = new() { GradientType = GradientType.Linear };
        private readonly List<Panel> _ticks = [];
        private double _fadeSeconds;
        private double _gap;
        private int _pulses;
        private bool _pulsing;

        public PreviewBox(UIContext context) : base(context)
        {
            Classes = ["action-preview-box"];
            Background = _fill;
        }

        public Vector4 From { get; set; }
        public Vector4 To { get; set; }

        /// <summary>At rest, show the whole fade left to right instead of only its start ("!bg").</summary>
        public bool Ramp { get; set; }

        /// <summary>At rest, one tick per pulse along the bottom ("!pulse"); 0 for none.</summary>
        public int Ticks
        {
            get;
            set
            {
                value = Math.Clamp(value, 0, MaxTicks);
                if (field == value) return;
                field = value;
                foreach (var tick in _ticks) RemoveChild(tick);
                _ticks.Clear();
                for (var i = 0; i < value; i++)
                {
                    var tick = new Panel(Context) { Classes = ["action-preview-tick"] };
                    _ticks.Add(tick);
                    AddChild(tick);
                }

                InvalidateLayout();
            }
        }

        /// <summary>Stops a run and shows the box at rest: the fade's ramp, or the starting color, at rest size.</summary>
        public void Rest()
        {
            _clock.Reset();
            _fill.Color = From;
            _ramp.GradientColors = [From, From, To, To];
            _ramp.GradientStops = [0, 0.15f, 0.85f, 1];
            Show(Ramp ? _ramp : _fill);
            Resize(1);
        }

        public void PlayFade(double seconds)
        {
            Rest();
            Show(_fill);
            _pulsing = false;
            _fadeSeconds = seconds;
            _clock.Start();
        }

        public void PlayPulses(int count, double gapSeconds)
        {
            Rest();
            _pulsing = true;
            _pulses = count;
            _gap = Math.Max(gapSeconds, PulseSeconds);
            _clock.Start();
        }

        public override void Update(UIContext uiContext)
        {
            base.Update(uiContext);
            if (!_clock.IsRunning) return;
            var t = _clock.Elapsed.TotalSeconds;

            if (!_pulsing)
            {
                var progress = _fadeSeconds <= 0 ? 1 : Math.Min(1, t / _fadeSeconds);
                _fill.Color = Vector4.Lerp(From, To, (float)progress);
                if (progress >= 1) _clock.Stop();
                return;
            }

            var pulse = (int)(t / _gap);
            if (pulse >= _pulses)
            {
                _clock.Stop();
                Resize(1);
                return;
            }

            var into = (t - pulse * _gap) / PulseSeconds;
            var rise = into < PulsePeak
                ? 1 - Math.Pow(1 - into / PulsePeak, 2) // ease-out up
                : into < 1
                    ? 1 - Math.Pow((into - PulsePeak) / (1 - PulsePeak), 2) // ease-in back
                    : 0;
            Resize(1 + (PulseScale - 1) * (float)rise);
        }

        /// <summary>Swaps the box's fill; the swap requeues, so it is skipped when nothing changes.</summary>
        private void Show(Renderable fill)
        {
            if (!ReferenceEquals(Background, fill)) Background = fill;
        }

        /// <summary>Spreads the ticks evenly along the bottom, 10 in from the sides.</summary>
        protected override void DoLayout()
        {
            const float inset = 10, bottom = 10;
            for (var i = 0; i < _ticks.Count; i++)
            {
                var t = _ticks.Count == 1 ? 0.5f : i / (float)(_ticks.Count - 1);
                _ticks[i].X = inset + t * Math.Max(0, Computed.Width - 2 * inset - _ticks[i].Width.Value);
                _ticks[i].Y = Computed.Height - bottom - _ticks[i].Height.Value;
            }

            base.DoLayout();
        }

        /// <summary>Scales the fill about the box's centre - what a pulse does to the page.</summary>
        private void Resize(float scale)
        {
            var w = Computed.Width;
            var h = Computed.Height;
            if (w <= 0 || h <= 0) return;
            _fill.SetPosition(new Vector3(Computed.AbsoluteX - w * (scale - 1) / 2, Computed.AbsoluteY - h * (scale - 1) / 2, 0));
            _fill.Scale = new Vector3(w * scale, h * scale, 1);
        }
    }
}

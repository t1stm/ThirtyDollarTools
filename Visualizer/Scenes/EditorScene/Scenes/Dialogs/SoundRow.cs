using OpenTK.Mathematics;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using VisualizerScene.Objects.Playfield.Batch.Chunks;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     One sound of the instrument being edited: its icon and name (click to select), its four
///     read-outs (value, volume, pan, offset) in the playfield badges' style, then Preview,
///     Duplicate and Remove buttons. The read-outs sit in their own band,
///     <see cref="NumberZone" />, which is the scroll zone that tunes the sound; the icon and
///     name pass the wheel on, so it scrolls the list instead.
/// </summary>
internal sealed class SoundRow : FlexPanel
{
    /// <summary>Value, volume, pan, offset: the read-outs' order, and the header chips'.</summary>
    public const int Columns = 4;

    private readonly Label[] _cells = new Label[Columns];
    private readonly FlexPanel[] _cellBoxes = new FlexPanel[Columns];
    private readonly bool[] _isDefault = new bool[Columns];
    private int? _highlighted;

    public SoundRow(UIContext context, InstrumentSound instance, SoundImage icon) : base(context)
    {
        Instance = instance;
        Icon = icon;
        Classes = ["sound-row"];

        var name = new FlexPanel(context)
        {
            Classes = ["sound-row-name"],
            Children = [new ClippedLabel(context, instance.Sound) { Classes = ["sound-row-name-label"] }]
        };
        Select = new FlexPanel(context)
        {
            Classes = ["sound-row-select"],
            UpdateCursorOnHover = true,
            OnClick = _ => OnSelect?.Invoke(this),
            Children = [icon, name]
        };

        NumberZone = new ScrollZone(context) { Classes = ["sound-row-zone"] };
        for (var i = 0; i < Columns; i++)
        {
            _cells[i] = new Label(context, "") { Classes = ["sound-row-cell-label"] };
            _cellBoxes[i] = new FlexPanel(context) { Classes = ["sound-row-cell"], Children = [_cells[i]] };
            NumberZone.AddChild(_cellBoxes[i]);
        }

        PreviewButton = IconButton(context, Icons.Play, () => OnPreview?.Invoke(this));
        DuplicateButton = IconButton(context, Icons.Copy, () => OnDuplicate?.Invoke(this));
        RemoveButton = IconButton(context, Icons.X, () => OnRemove?.Invoke(this));

        PreviewButton.Label.Classes.Add("sound-row-button-play");

        // Lead and tail stand in for left and right padding: a row's padding is one value,
        // and its read-out band has to reach the row's full height.
        Children =
        [
            new Panel(context) { Classes = ["sound-row-lead"] },
            Select, NumberZone, PreviewButton, DuplicateButton, RemoveButton,
            new Panel(context) { Classes = ["sound-row-tail"] }
        ];
        Refresh();
    }

    public InstrumentSound Instance { get; }

    /// <summary>The sound's image; the owner releases it when the row goes.</summary>
    public SoundImage Icon { get; }

    /// <summary>The icon and name: a click here selects the row.</summary>
    public FlexPanel Select { get; }

    /// <summary>The read-outs' band, which scroll gestures tune the sound over.</summary>
    public ScrollZone NumberZone { get; }

    public Button PreviewButton { get; }
    public Button DuplicateButton { get; }
    public Button RemoveButton { get; }

    public Action<SoundRow>? OnSelect { get; set; }
    public Action<SoundRow>? OnPreview { get; set; }
    public Action<SoundRow>? OnDuplicate { get; set; }
    public Action<SoundRow>? OnRemove { get; set; }

    /// <summary>The read-outs' current text, in <see cref="Columns" /> order.</summary>
    public IEnumerable<string> CellTexts => _cells.Select(cell => cell.Value.ToString());

    public void SetSelected(bool selected)
    {
        SetClass("sound-row-selected", selected);
    }

    /// <summary>Rewrites the read-outs from <see cref="Instance" />. A default shows as a dimmed dash.</summary>
    public void Refresh()
    {
        string?[] texts = [ValueText(Instance.Value), VolumeText(Instance.Volume), PanText(Instance.Pan),
            OffsetText(Instance.Offset)];

        for (var i = 0; i < Columns; i++)
        {
            _cells[i].Value = texts[i] ?? "\u2014";
            _isDefault[i] = texts[i] == null;
        }

        Highlight(_highlighted);
    }

    /// <summary>
    ///     The read-out column a scroll would change, lit, or none. The editor drives this from
    ///     the held modifiers while the pointer is over <see cref="NumberZone" />.
    /// </summary>
    public void Highlight(int? column)
    {
        _highlighted = column;
        NumberZone.SetClass("sound-row-zone-hovered", column != null);
        for (var i = 0; i < Columns; i++)
        {
            _cellBoxes[i].SetClass("sound-row-cell-target", column == i);
            // The lit cell reads at full strength even at its default: it is the one about to change.
            _cells[i].SetClass("sound-row-cell-default", _isDefault[i] && column != i);
        }
    }

    // Read-outs in the playfield badges' words (RenderableFactory), null for a default.
    internal static string? ValueText(double value) =>
        RenderableFactory.FormatValueText(new NormalEvent { SoundEvent = "", Value = value });

    internal static string? VolumeText(double? volume) =>
        RenderableFactory.FormatVolumeText(new NormalEvent { Volume = volume });

    /// <summary>
    ///     The pan badge's arrows, but on the editor's -100..100 scale rather than the badge's
    ///     TDW tenths, so the read-out says the same number as the Pan field beside it.
    /// </summary>
    internal static string? PanText(float pan) => pan switch
    {
        > 0 => $"{pan:0.##}>",
        < 0 => $"<{-pan:0.##}",
        _ => null
    };

    internal static string? OffsetText(double offset) => offset == 0 ? null : $"{offset:0.###} s";

    private static Button IconButton(UIContext context, string icon, Action onClick)
    {
        var button = new Button(context, icon, font: Icons.Font) { Classes = ["sound-row-button"], OnClick = _ => onClick() };
        button.Label.Classes = ["sound-row-button-icon"];
        return button;
    }

    /// <summary>
    ///     A label cut off at its parent's edges. A Sundex label neither wraps nor ellipsizes,
    ///     and a long sound name would otherwise paint over the read-outs beside it.
    /// </summary>
    private sealed class ClippedLabel(UIContext context, string text) : Label(context, text)
    {
        public override void ApplyClip(Vector4i? clip)
        {
            if (Parent is not { } parent)
            {
                base.ApplyClip(clip);
                return;
            }

            var box = parent.Computed;
            var x = (int)box.AbsoluteX;
            var y = (int)box.AbsoluteY;
            base.ApplyClip(IntersectClip(new Vector4i(x, y, x + (int)box.Width, y + (int)box.Height), clip));
        }
    }
}

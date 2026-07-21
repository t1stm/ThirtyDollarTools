using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     One instrument-list row: clicking it picks the instrument; a separate "edit"
///     button opens the name/sounds form instead. Pure view — <see cref="InstrumentSelector" />
///     owns both callbacks.
/// </summary>
public sealed class InstrumentRow : FlexPanel
{
    private static readonly Vector4 RowColor = new(0.086f, 0.086f, 0.118f, 1f); // #16161e
    private static readonly Vector4 LightColor = new(0.478f, 0.635f, 0.968f, 1f); // #7aa2f7, the app's light accent

    public InstrumentRow(UIContext context, Instrument instrument, Action<Instrument> onPick,
        Action<Instrument> onEdit) : base(context)
    {
        Instrument = instrument;
        Direction = LayoutDirection.Horizontal;
        VerticalAlign = Align.Center;
        Width = LiteralOrComputable.Percent(100);
        Height = 36;
        Padding = 6;
        Spacing = 10;
        BorderRadius = 6;
        Background = new ColoredPlane { Color = RowColor };
        UpdateCursorOnHover = true;
        OnClick = _ => onPick(instrument);

        var edit = new Button(context, "Edit", new ColoredPlane { Color = LightColor })
        {
            FontSizePx = 12f,
            BorderRadius = 6,
            OnClick = _ => onEdit(instrument),
            Label = { Color = RowColor }
        };

        // Percent-width spacer soaks up the free space so Edit lands flush against the
        // row's right edge — this framework has no space-between align.
        var spacer = new Panel(context) { Width = LiteralOrComputable.Percent(100) };

        Children =
        [
            new Label(context, instrument.Name) { FontSizePx = 14f },
            spacer,
            edit
        ];
    }

    public Instrument Instrument { get; }
}

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
///     button opens the name/sounds form instead. Pure view - <see cref="InstrumentSelector" />
///     owns both callbacks.
/// </summary>
public sealed class InstrumentRow : FlexPanel
{
    private static readonly Vector4 RowColor = EditorPalette.Panel;
    private static readonly Vector4 LightColor = EditorPalette.Header; // the app's light accent
    private static readonly Vector4 DeleteColor = EditorPalette.DangerAccent; // same red as LaneHeader's mute indicator

    private readonly Label _name;

    public InstrumentRow(UIContext context, Instrument instrument, Action<Instrument> onPick,
        Action<Instrument> onEdit, Action<Instrument> onDelete) : base(context)
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

        var delete = new Button(context, "Delete", new ColoredPlane { Color = DeleteColor })
        {
            FontSizePx = 12f,
            BorderRadius = 6,
            OnClick = _ => onDelete(instrument),
            Label = { Color = RowColor }
        };

        // The name soaks up the free space itself so Edit/Delete land flush against the
        // row's right edge (this framework has no space-between align) - and a long name
        // can no longer push them out of the row, since a percent width ignores the text's
        // own measured size. Overflowing text is clipped in ApplyClip below.
        _name = new Label(context, instrument.Name)
        {
            FontSizePx = 14f,
            Width = LiteralOrComputable.Percent(100) // after FontSizePx: its setter rewrites Width
        };

        Children = [_name, edit, delete];
    }

    public Instrument Instrument { get; }

    /// <summary>Cuts the name off at its own box so a long one never paints over Edit/Delete.</summary>
    public override void ApplyClip(Vector4i? clip)
    {
        base.ApplyClip(clip);

        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        var right = (int)(_name.Computed.AbsoluteX + _name.Computed.Width);
        _name.ApplyClip(IntersectClip(new Vector4i(x, y, right, y + (int)Computed.Height), clip));
    }
}
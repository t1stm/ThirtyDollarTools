using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Style.DSL;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     One instrument-list row: clicking it picks the instrument; a separate "edit"
///     button opens the name/sounds form instead. Pure view - <see cref="InstrumentSelector" />
///     owns both callbacks.
/// </summary>
public sealed class InstrumentRow : FlexPanel
{
    private readonly Label _name;

    public InstrumentRow(UIContext context, Instrument instrument, Action<Instrument> onPick,
        Action<Instrument> onEdit, Action<Instrument> onDelete) : base(context)
    {
        Instrument = instrument;
        Classes = ["instrument-row"];
        UpdateCursorOnHover = true;
        OnClick = _ => onPick(instrument);

        var edit = new Button(context, "Edit")
        {
            Classes = ["row-button-light"],
            OnClick = _ => onEdit(instrument),
            Label = { Classes = ["dark-label"] }
        };

        var delete = new Button(context, "Delete")
        {
            Classes = ["row-button-danger"],
            OnClick = _ => onDelete(instrument),
            Label = { Classes = ["dark-label"] }
        };

        // The name takes a percent width rather than its measured one, so it soaks up the
        // free space (this framework has no space-between align) and a long name can never
        // push Edit/Delete out of the row. Overflowing text is clipped in ApplyClip, and the
        // width itself is applied in ApplyStyleSheet, both below.
        _name = new Label(context, instrument.Name) { Classes = ["body-label"] };

        Children = [_name, edit, delete];
    }

    public Instrument Instrument { get; }

    /// <summary>
    ///     Re-claims the name's full width after styling. Label's <c>font-size</c> setter
    ///     rewrites Width to the measured text, so the width cannot come from the sheet.
    /// </summary>
    public override void ApplyStyleSheet(StyleSheet styleSheet)
    {
        base.ApplyStyleSheet(styleSheet);
        _name.Width = LiteralOrComputable.Percent(100);
    }

    /// <summary>Clips the name to its own box so a long one never paints over Edit/Delete.</summary>
    public override void ApplyClip(Vector4i? clip)
    {
        base.ApplyClip(clip);

        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        var right = (int)(_name.Computed.AbsoluteX + _name.Computed.Width);
        _name.ApplyClip(IntersectClip(new Vector4i(x, y, right, y + (int)Computed.Height), clip));
    }
}
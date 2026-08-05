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

        // The name soaks up the free space itself so Edit/Delete land flush against the
        // row's right edge (this framework has no space-between align) - and a long name
        // can no longer push them out of the row, since a percent width ignores the text's
        // own measured size. Overflowing text is clipped in ApplyClip below.
        //
        // The width has to be re-set after the style pass, not just declared on a class:
        // Label's FontSizePx setter rewrites Width to the measured text, and the sheet
        // applies font-size and width in reflection order - so a class holding both could
        // land the wrong way round. See the ApplyStyleSheet override below.
        _name = new Label(context, instrument.Name) { Classes = ["body-label"] };

        Children = [_name, edit, delete];
    }

    public Instrument Instrument { get; }

    /// <summary>
    ///     Re-claims the name's full width after styling. Applying <c>font-size</c> makes
    ///     Label remeasure and overwrite its own Width, and the sheet sets properties in
    ///     reflection order - so this can't just be a <c>width</c> on the label's class.
    /// </summary>
    public override void ApplyStyleSheet(StyleSheet styleSheet)
    {
        base.ApplyStyleSheet(styleSheet);
        _name.Width = LiteralOrComputable.Percent(100);
    }

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
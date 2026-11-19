using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Panels;

namespace Sundex.Components.Labels;

[Sundex.Components.Attributes.MarkupTag("button")]
public class Button : FlexPanel
{
    /// <param name="font">The label's font; null for the interface font. See <see cref="Label" />.</param>
    public Button(UIContext context, string label, Renderable? background = null, string? font = null) : this(
        context, new Label(context, label, font), background)
    {
    }

    /// <summary>
    ///     A button with no label of its own, for markup: its children are whatever the markup puts
    ///     in it. <see cref="Label" /> is a detached placeholder until something sets it.
    /// </summary>
    public Button(UIContext context) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Padding = 5;
        HorizontalAlign = Align.Center;
        VerticalAlign = Align.Center;
        Label = new Label(context);
        UpdateCursorOnHover = true;
        Focusable = true;
    }

    public Button(UIContext context, Label label, Renderable? background = null) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Padding = 5;
        HorizontalAlign = Align.Center;
        VerticalAlign = Align.Center;
        Background = background;

        Children = [Label = label];
        UpdateCursorOnHover = true;
        Focusable = true;
    }


    public override string Tag => "button";

    /// <summary>Enter and Space press a focused button, as in a browser.</summary>
    public override bool HandleKeyDown(OpenTK.Windowing.Common.KeyboardKeyEventArgs args)
    {
        if (Disabled || args.IsRepeat ||
            args.Key is not (OpenTK.Windowing.GraphicsLibraryFramework.Keys.Enter
                or OpenTK.Windowing.GraphicsLibraryFramework.Keys.KeyPadEnter
                or OpenTK.Windowing.GraphicsLibraryFramework.Keys.Space))
            return false;
        OnClick?.Invoke(this);
        return true;
    }
    public Label Label { get; set; }

    [NamedSetting("text-value")]
    public ReadOnlySpan<char> Value
    {
        get => Label.Value;
        set => Label.Value = value;
    }

    [NamedSetting("font-size")]
    public LiteralOrComputable FontSizePx
    {
        get => Label.FontSizePx;
        set => Label.FontSizePx = value;
    }

}
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Panels;

namespace Sundex.Components.Labels;

public class Button : FlexPanel
{
    public Button(UIContext context, string label, Renderable? background = null) : this(context,
        new Label(context, label)
        {
            AnchorX = Anchor.Center, AnchorY = Anchor.Center
        }, background)
    {
    }

    public Button(UIContext context, Label label, Renderable? background = null) : base(context)
    {
        HorizontalAlign = Align.Center;
        VerticalAlign = Align.Center;
        Background = background;

        Children = [Label = label];
        UpdateCursorOnHover = true;
    }

    public override LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;
    public override float Padding { get; set; } = 5;

    public override string Tag => "button";
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

    [NamedSetting("width")] public override LiteralOrComputable Width { get; set; } = new(0, false, true);
}
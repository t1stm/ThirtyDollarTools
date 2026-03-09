using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Color_Scheme;
using Sundex.Components.Panels;

namespace Sundex.Components.Labels;

public class Button : FlexPanel
{
    public override string Tag => "button";
    public Label Label { get; set; }

    public Button(UIContext context, string label, Renderable? background = null) : this(context,
        new Label(context, label), background)
    {
    }

    public Button(UIContext context, Label label, Renderable? background = null) : base(context)
    {
        Padding = 5;
        HorizontalAlign = Align.Center;
        VerticalAlign = Align.Center;
        Background = background ?? new ColoredPlane
        {
            Color = DarkScheme.AccentBlue,
            BorderRadius = 10
        };
        
        Children = [Label = label];
        UpdateCursorOnHover = true;
    }

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

    [NamedSetting("width")]
    public override LiteralOrComputable Width { get; set; } = new(0, false, true);
}
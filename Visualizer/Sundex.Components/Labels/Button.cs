using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
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
        AutoSizeSelf = true;
        Padding = 5;
        Background = background ?? new ColoredPlane
        {
            Color = DarkScheme.AccentBlue,
            BorderRadius = 10
        };

        Children = [Label = label];
        UpdateCursorOnHover = true;
    }

    public ReadOnlySpan<char> Value
    {
        get => Label.Value;
        set => Label.Value = value;
    }

    public float FontSizePx
    {
        get => Label.FontSizePx;
        set => Label.FontSizePx = value;
    }
}
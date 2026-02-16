using Shared.Renderer;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Color_Scheme;
using Sundex.Components.Panels;

namespace Sundex.Components.Labels;

public class Button : FlexPanel
{
    private readonly Label _label;

    public Button(UIContext context, string label, Renderable? background = null) : this(context,
        new Label(context, label), background)
    {
    }

    public Button(UIContext context, Label label, Renderable? background = null) : base(context)
    {
        AutoSizeSelf = true;
        AutoWidth = true;
        AutoHeight = true;

        Padding = 5;
        Background = background ?? new ColoredPlane
        {
            Color = DarkScheme.AccentBlue,
            BorderRadius = 10
        };

        Children = [_label = label];
        UpdateCursorOnHover = true;
    }

    public ReadOnlySpan<char> Value
    {
        get => _label.Value;
        set => _label.Value = value;
    }

    public float FontSizePx
    {
        get => _label.FontSizePx;
        set => _label.FontSizePx = value;
    }
}
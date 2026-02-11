using Components.Abstractions;
using Components.Panels;
using Shared.Renderer;
using Shared.Renderer.Planes;

namespace Components.Labels;

public class Button : FlexPanel
{
    private readonly Label _label;

    public Button(UIContext context, string label, Renderable? background = null) : base(context)
    {
        AutoSizeSelf = true;
        AutoWidth = true;
        AutoHeight = true;

        Padding = 5;
        Background = background ?? new ColoredPlane
        {
            Color = (0.2f, 0.2f, 0.2f, 1.0f)
        };

        Children = [_label = new Label(context, label)];
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

    public void SetTextContents(string text)
    {
        _label.SetTextContents(text);
    }
}
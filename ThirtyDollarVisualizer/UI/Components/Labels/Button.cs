using OpenTK.Mathematics;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.UI;

public class Button : FlexPanel, IText
{
    private readonly Label _label;
    public Button(string label, Vector4? background = null)
    {
        AutoSizeSelf = true;
        AutoWidth = true;
        AutoHeight = true;
        
        Padding = 5;
        Background = new ColoredPlane
        {
            Color = background ?? (0.2f, 0.2f, 0.2f, 1.0f)
        }; 
        
        Children = [_label = new Label(label)];
        UpdateCursorOnHover = true;
    }

    public string Value
    {
        get => _label.Value;
        set => _label.Value = value;
    }

    public float FontSizePx
    {
        get => _label.FontSizePx;
        set => _label.FontSizePx = value;
    }

    public FontStyle FontStyle
    {
        get => _label.FontStyle;
        set => _label.FontStyle = value;
    }

    public void SetTextContents(string text)
    {
        _label.SetTextContents(text);
    }
}
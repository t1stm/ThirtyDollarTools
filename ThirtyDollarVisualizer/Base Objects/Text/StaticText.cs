using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects.Text;

/// <summary>
/// A renderable that is quick to render, intended to be used for text that is static, and changed rarely.
/// </summary>
public class StaticText : TexturedPlane, IText
{
    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set => SetTextContents(value);
    }
    public FontStyle FontStyle { get; set; } = FontStyle.Regular;
    
    private float _font_size = 14f;
    public float FontSizePx
    {
        get => _font_size;
        set => SetFontSize(value);
    }
    
    public void SetFontSize(float font_size_px)
    {
        _font_size = font_size_px;
        SetTextContents(Value);
    }

    public void SetTextContents(string text)
    {
        if (_value == text) return;
        _value = text;
        var family = Fonts.GetFontFamily();
        var font = family.CreateFont(FontSizePx, FontStyle);

        var texture = new Texture(font, text);
        SetTexture(texture);
        SetScale((texture.Width, texture.Height, 1));
    }
}
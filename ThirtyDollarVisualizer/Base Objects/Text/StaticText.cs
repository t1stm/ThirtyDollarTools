using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Objects.Text;

/// <summary>
///     A renderable that is quick to render, intended to be used for text that is static, and changed rarely.
/// </summary>
public class StaticText(FontFamily? font_family = null) : TexturedPlane, IText
{
    private float _font_size = 14f;
    private string _value = string.Empty;

    public string Value
    {
        get => _value;
        set => SetTextContents(value);
    }

    public FontStyle FontStyle { get; set; } = FontStyle.Regular;

    public float FontSizePx
    {
        get => _font_size;
        set => SetFontSize(value);
    }

    public void SetTextContents(string text)
    {
        if (_value == text) return;
        _value = text;
        var family = font_family ?? Fonts.GetFontFamily();
        var font = family.CreateFont(FontSizePx, FontStyle);

        var texture = new FontTexture(font, text);
        SetTexture(texture);
        SetScale((texture.Width, texture.Height, 1));
    }

    public void SetFontSize(float font_size_px)
    {
        _font_size = font_size_px;
        SetTextContents(Value);
    }
}
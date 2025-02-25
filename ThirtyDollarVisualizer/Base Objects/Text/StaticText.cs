using OpenTK.Mathematics;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Objects.Text;

/// <summary>
///     A renderable that is quick to render, intended to be used for text that is static, and changed rarely.
/// </summary>
public class StaticText(FontFamily? font_family = null) : ITextRenderable
{
    public StaticText() : this(null)
    {
    }
    
    private readonly TexturedPlane _texturedPlane = new();
    private float _font_size = 14f;
    private string _value = string.Empty;

    public override string Value
    {
        get => _value;
        set => SetTextContents(value);
    }

    public override FontStyle FontStyle { get; set; } = FontStyle.Regular;

    public override float FontSizePx
    {
        get => _font_size;
        set => SetFontSize(value);
    }

    public override void SetTextContents(string text)
    {
        if (_value == text) return;
        _value = text;
        var family = font_family ?? Fonts.GetFontFamily();
        var font = family.CreateFont(FontSizePx, FontStyle);

        var texture = new FontTexture(font, text);
        _texturedPlane.SetTexture(texture);
        _texturedPlane.SetScale((texture.Width, texture.Height, 1));
    }

    public override void Render(Camera camera)
    {
        _texturedPlane.IsVisible = IsVisible;
        _texturedPlane.Render(camera);
    }

    public override void SetPosition(Vector3 position, PositionAlign align = PositionAlign.TopLeft)
    {
        _texturedPlane.SetPosition(position, align);
    }

    public void SetFontSize(float font_size_px)
    {
        _font_size = font_size_px;
        SetTextContents(Value);
    }
}
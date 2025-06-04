using OpenTK.Mathematics;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Base_Objects.Text;

/// <summary>
///     A renderable that is quick to render, intended to be used for text that is static, and changed rarely.
/// </summary>
public class StaticText(FontFamily? fontFamily = null) : TextRenderable
{
    private const int YOffset = 7;

    private readonly TexturedPlane _texturedPlane = new();
    private float _fontSize = 14f;
    private string _value = string.Empty;

    public StaticText() : this(null)
    {
    }

    public override string Value
    {
        get => _value;
        set => SetTextContents(value);
    }

    public override FontStyle FontStyle { get; set; } = FontStyle.Regular;

    public override float FontSizePx
    {
        get => _fontSize;
        set => SetFontSize(value);
    }

    public override Vector3 Scale
    {
        get => base.Scale;
        set
        {
            _texturedPlane.Scale = value;
            value.Y -= YOffset;
            base.Scale = value;
        }
    }

    public override void SetTextContents(string text)
    {
        if (_value == text) return;
        _value = text;
        var family = fontFamily ?? Fonts.GetFontFamily();
        var font = family.CreateFont(FontSizePx, FontStyle);

        var texture = new FontTexture(font, text);
        _texturedPlane.SetTexture(texture);
        Scale = (texture.Width, texture.Height, 1);
    }

    public override void Render(Camera camera)
    {
        _texturedPlane.IsVisible = IsVisible;
        _texturedPlane.Render(camera);
    }

    public override void SetPosition(Vector3 position, PositionAlign align = PositionAlign.TopLeft)
    {
        position.X = MathF.Round(position.X);
        position.Y = MathF.Round(position.Y);
        position.Z = MathF.Round(position.Z);

        _texturedPlane.SetPosition(position, align);
    }

    public void SetFontSize(float fontSizePx)
    {
        _fontSize = fontSizePx;
        SetTextContents(Value);
    }
}
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects.Text;

/// <summary>
/// A renderable that is more expensive to be rendered, intended to be used for text that is changed often.
/// </summary>
public class DynamicText : Renderable, IText
{
    public string Value
    {
        get => _value;
        set => SetTextContents(value);
    }

    public FontStyle FontStyle { get; set; } = FontStyle.Regular;
    
    private string _value = string.Empty;
    public float FontSizePx { get; private set; } = 14f;
    private TexturedPlane[] TexturedPlanes = Array.Empty<TexturedPlane>();

    public void SetFontSize(float font_size_px)
    {
        FontSizePx = font_size_px;
        SetTextContents(Value);
    }

    public void SetTextContents(string text)
    {
        if (_value == text) return;
        var cache = Fonts.GetCharacterCache();
        var textures = text.Select(c => cache.Get(c, FontSizePx, FontStyle)).ToArray();
        TexturedPlanes = new TexturedPlane[textures.Length];
        
        var x = _position.X;
        var y = _position.Y;
        var z = _position.Z;
        
        var start_X = x;
        var max_x = 0f;
        var max_y = 0f;
        
        for (var i = 0; i < textures.Length; i++)
        {
            var texture = textures[i];
            var character = text[i];
            var w = texture.Width;
            var h = texture.Height;

            if (character == '\n')
            {
                y += FontSizePx;
                x = start_X;
            }

            var plane = new TexturedPlane(texture, (x, y, z), (w, h));
            TexturedPlanes[i] = plane;
            
            x += w;
            max_x = Math.Max(max_x, x);
            max_y = Math.Max(max_y, y + h);
        }

        _scale = (max_x, max_y, 1);
        _value = text;
    }

    public override void Render(Camera camera)
    {
        foreach (var plane in TexturedPlanes)
        {
            plane.Render(camera);
        }
        
        base.Render(camera);
    }
}
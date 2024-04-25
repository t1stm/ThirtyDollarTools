using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects.Text;

public class BasicDynamicText : CachedDynamicText
{
    private TexturedPlane? StaticPlane;
    
    public override void SetTextContents(string text)
    {
        _value = text;
    }

    public override void Render(Camera camera)
    {
        StaticPlane ??= new TexturedPlane(Texture.Transparent1x1, (0,0,0), (1,1,1));
        
        var text = _value;
        var x = _position.X;
        var y = _position.Y;
        var z = _position.Z;

        var start_X = x;
        var max_x = 0f;
        var max_y = 0f;
        
        var cache = Fonts.GetCharacterCache();
        
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            // I am using string here since emojis take multiple char objects to be stored.
            string? emoji = null;
            if (char.IsSurrogate(c) && i + 1 < text.Length && char.IsSurrogatePair(c, text[i + 1]))
            {
                emoji = text.Substring(i, 2);
                i++;
            }

            if (c == '\n')
            {
                y += FontSizePx;
                x = start_X;
                continue;
            }
            
            var texture = emoji != null
                ? cache.GetEmoji(emoji, _font_size_px, FontStyle)
                : cache.Get(c, _font_size_px, FontStyle);
            var w = texture.Width;
            var h = texture.Height;
            
            StaticPlane.SetPosition((x,y,z));
            StaticPlane.SetScale((w, h, 0));
            StaticPlane.SetTexture(texture);
            StaticPlane.Render(camera);
            
            x += w;
            max_x = Math.Max(max_x, x);
            max_y = Math.Max(max_y, y + h);
        }
        
        _scale = (max_x, max_y, 1);
    }
}
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Textures.Static;

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
        if (!IsVisible) return;
        
        StaticPlane ??= new TexturedPlane(StaticTexture.Transparent1x1, (0, 0, 0), (1, 1, 1));

        ReadOnlySpan<char> text = _value;
        var x = Position.X;
        var y = Position.Y;
        var z = Position.Z;

        var start_X = x;
        var max_x = 0f;
        var max_y = 0f;

        var cache = Fonts.GetCharacterCache();

        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            // I am using string here since emojis take multiple char objects to be stored.
            ReadOnlySpan<char> emoji = [];
            if (char.IsSurrogate(c) && i + 1 < text.Length && char.IsSurrogatePair(c, text[i + 1]))
            {
                emoji = text.Slice(i, 2);
                i++;
            }

            if (c == '\n')
            {
                y += FontSizePx;
                x = start_X;
                lines++;
                continue;
            }

            var texture = emoji.Length > 0
                ? cache.GetEmoji(emoji, _font_size_px, FontStyle)
                : cache.Get(c, _font_size_px, FontStyle);
            var w = texture.Width;
            var h = texture.Height;

            StaticPlane.SetPosition((x, y, z));
            StaticPlane.Scale = (w, h, 0);
            StaticPlane.SetTexture(texture);
            StaticPlane.Render(camera);

            x += w;
            max_x = Math.Max(max_x, x);
            max_y = Math.Max(max_y, y + h);
        }

        Scale = (max_x, lines * _font_size_px, 1);
    }
}
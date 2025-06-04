using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Base_Objects.Text;

public class BasicDynamicText : CachedDynamicText
{
    private TexturedPlane? _staticPlane;
    public override string Value { get; set; } = string.Empty;

    public override void Render(Camera camera)
    {
        if (!IsVisible) return;

        _staticPlane ??= new TexturedPlane(StaticTexture.TransparentPixel)
        {
            Position = Vector3.Zero,
            Scale = Vector3.One
        };

        ReadOnlySpan<char> text = Value;
        var x = Position.X;
        var y = Position.Y;
        var z = Position.Z;

        var start_x = x;
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
                x = start_x;
                lines++;
                continue;
            }

            var texture = emoji.Length > 0
                ? cache.GetEmoji(emoji, FontSizePx, FontStyle)
                : cache.Get(c, FontSizePx, FontStyle);
            var w = texture.Width;
            var h = texture.Height;

            _staticPlane.SetPosition((x, y, z));
            _staticPlane.Scale = (w, h, 0);
            _staticPlane.SetTexture(texture);
            _staticPlane.Render(camera);

            x += w;
            max_x = Math.Max(max_x, x);
            max_y = Math.Max(max_y, y + h);
        }

        Scale = (max_x, lines * FontSizePx, 1);
    }
}
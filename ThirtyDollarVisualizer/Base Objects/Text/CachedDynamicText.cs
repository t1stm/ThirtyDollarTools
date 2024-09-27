using OpenTK.Mathematics;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects.Text;

/// <summary>
///     A renderable that is more expensive to be rendered but cheap to be edited, intended to be used for text that is
///     changed often.
/// </summary>
public class CachedDynamicText : Renderable, IText
{
    private readonly SemaphoreSlim _lock = new(1);
    private readonly HashSet<int> NewLineIndices = [];
    protected float _font_size_px = 14f;
    protected string _value = string.Empty;

    private TexturedPlane[] TexturedPlanes = [];

    public string Value
    {
        get => _value;
        set => SetTextContents(value);
    }

    public float FontSizePx
    {
        get => _font_size_px;
        set => SetFontSize(value);
    }

    public FontStyle FontStyle { get; set; } = FontStyle.Regular;

    public virtual void SetTextContents(string text)
    {
        if (_value == text) return;
        SetTextTextures(text);
        _value = text;
    }

    public void SetFontSize(float font_size_px)
    {
        _font_size_px = font_size_px;
        SetTextContents(Value);
    }

    protected void SetTextTextures(string text)
    {
        NewLineIndices.Clear();
        var cache = Fonts.GetCharacterCache();
        var textures_array = new Texture[text.Length];
        var real_i = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsSurrogate(c) && i + 1 < text.Length && char.IsSurrogatePair(c, text[i + 1]))
            {
                var emoji = text.Substring(i, 2);
                textures_array[real_i++] = cache.GetEmoji(emoji, _font_size_px, FontStyle);
                i++;
                continue;
            }

            if (c == '\n') NewLineIndices.Add(real_i);

            textures_array[real_i++] = cache.Get(c, _font_size_px, FontStyle);
        }

        var textures = textures_array.AsSpan()[..real_i];
        _lock.Wait();
        try
        {
            if (TexturedPlanes.Length != textures.Length)
            {
                TexturedPlanes = new TexturedPlane[textures.Length];
                for (var index = 0; index < TexturedPlanes.Length; index++)
                    TexturedPlanes[index] = new TexturedPlane(Texture.Transparent1x1,
                        (0, 0, 0), (1, 1));
            }

            var x = _position.X;
            var y = _position.Y;
            var z = _position.Z;

            var start_X = x;
            var max_x = 0f;
            var max_y = 0f;

            for (var i = 0; i < textures.Length; i++)
            {
                var texture = textures[i];
                var w = texture.Width;
                var h = texture.Height;

                if (NewLineIndices.Contains(i))
                {
                    y += FontSizePx;
                    x = start_X;
                }

                var plane = TexturedPlanes[i];

                plane.SetTexture(texture);
                plane.SetPosition((x, y, z));
                plane.SetScale((w, h, 0));

                x += w;
                max_x = Math.Max(max_x, x);
                max_y = Math.Max(max_y, y + h);
            }

            _scale = (max_x, max_y, 1);
        }
        finally
        {
            _lock.Release();
        }
    }

    public override void SetPosition(Vector3 position, PositionAlign align = PositionAlign.TopLeft)
    {
        base.SetPosition(position, align);
        SetTextTextures(_value);
    }

    public override void Render(Camera camera)
    {
        _lock.Wait();
        foreach (var plane in TexturedPlanes) plane.Render(camera);

        _lock.Release();

        base.Render(camera);
    }
}
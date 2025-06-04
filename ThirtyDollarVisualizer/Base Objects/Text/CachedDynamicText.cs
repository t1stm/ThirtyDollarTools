using OpenTK.Mathematics;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Base_Objects.Text;

/// <summary>
///     A renderable that is more expensive to be rendered but inexpensive to be edited, intended to be used for text
///     changed often.
/// </summary>
public class CachedDynamicText : TextRenderable
{
    private readonly SemaphoreSlim _lock = new(1);
    private readonly HashSet<int> _newLineIndices = [];
    private float _fontSizePx = 14f;

    private TexturedPlane[] _texturedPlanes = [];
    private string _value = string.Empty;

    public override string Value
    {
        get => _value;
        set
        {
            _value = value;
            SetTextTextures(value);
        }
    }

    public override float FontSizePx
    {
        get => _fontSizePx;
        set => SetFontSize(value);
    }

    public override FontStyle FontStyle { get; set; } = FontStyle.Regular;

    public void SetFontSize(float fontSizePx)
    {
        _fontSizePx = fontSizePx;
        SetTextTextures(Value);
    }

    protected virtual void SetTextTextures(ReadOnlySpan<char> text)
    {
        _newLineIndices.Clear();
        var cache = Fonts.GetCharacterCache();
        var textures_array = new SingleTexture[text.Length];
        var real_i = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsSurrogate(c) && i + 1 < text.Length && char.IsSurrogatePair(c, text[i + 1]))
            {
                var emoji = text.Slice(i, 2);
                textures_array[real_i++] = cache.GetEmoji(emoji, _fontSizePx, FontStyle);
                i++;
                continue;
            }

            if (c == '\n') _newLineIndices.Add(real_i);

            textures_array[real_i++] = cache.Get(c, _fontSizePx, FontStyle);
        }

        var textures = textures_array.AsSpan()[..real_i];
        _lock.Wait();
        try
        {
            if (_texturedPlanes.Length != textures.Length)
            {
                _texturedPlanes = new TexturedPlane[textures.Length];
                for (var index = 0; index < _texturedPlanes.Length; index++)
                    _texturedPlanes[index] = new TexturedPlane(StaticTexture.TransparentPixel);
            }

            var x = MathF.Round(Position.X);
            var y = MathF.Round(Position.Y);
            var z = MathF.Round(Position.Z);

            var start_x = x;
            var max_x = 0f;
            var max_y = 0f;

            var lines = 1;
            for (var i = 0; i < textures.Length; i++)
            {
                var texture = textures[i];
                var w = texture.Width;
                var h = texture.Height;

                if (_newLineIndices.Contains(i))
                {
                    y += FontSizePx;
                    x = start_x;
                    lines++;
                }

                var plane = _texturedPlanes[i];

                plane.SetTexture(texture);
                plane.SetPosition((x, y, z));
                plane.Scale = (w, h, 0);

                x += w;
                max_x = Math.Max(max_x, x);
                max_y = Math.Max(max_y, y + h);
            }

            Scale = (max_x, lines * _fontSizePx, 1);
        }
        finally
        {
            _lock.Release();
        }
    }

    public override void SetPosition(Vector3 position, PositionAlign align = PositionAlign.TopLeft)
    {
        _lock.Wait();
        base.SetPosition(position, align);
        _lock.Release();
        SetTextTextures(_value);
    }

    public override void Render(Camera camera)
    {
        if (!IsVisible) return;

        _lock.Wait();
        foreach (var plane in _texturedPlanes.AsSpan()) plane.Render(camera);
        _lock.Release();

        base.Render(camera);
    }
}
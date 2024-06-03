using System.Reflection;
using OpenTK.Graphics.OpenGL;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThirtyDollarVisualizer.Helpers.Textures;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.Objects;

public class Texture : IDisposable
{
    private static Texture? _transparent1x1;

    private int? _handle;
    public int Height;
    private Image<Rgba32>? texture_data;
    public int Width;

    public Texture(string path)
    {
        Stream source;

        if (File.Exists(path))
        {
            source = File.OpenRead(path);
        }
        else
        {
            var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(path);

            source = stream ??
                     throw new FileNotFoundException($"Unable to find texture \'{path}\' in assembly or real path.");
        }

        texture_data = Image.Load<Rgba32>(source);
        Width = texture_data.Width;
        Height = texture_data.Height;

        source.Dispose();
    }

    public Texture(Font font, string text, Color? color = null)
    {
        var options = new TextOptions(font)
        {
            FallbackFontFamilies = new[]
            {
                Fonts.GetEmojiFamily()
            }
        };
        var rect = TextMeasurer.MeasureAdvance(text, options);

        var c_w = Math.Ceiling(rect.Width);
        // magic number because imagesharp broke the MeasureSize method.
        var c_h = Math.Ceiling(rect.Height + 7);

        texture_data = new Image<Rgba32>((int)Math.Max(c_w, 1), (int)Math.Max(c_h, 1), Color.Transparent);
        Width = texture_data.Width;
        Height = texture_data.Height;

        var point = PointF.Empty;

        color ??= Color.White;

        var cast_color = color.Value;

        texture_data.Mutate(x =>
            x.DrawText(text, font, Color.Black, point)
                .GaussianBlur(1f)
                .DrawText(text, font, cast_color, point)
        );
    }

    public Texture(Font font, Color circle_color, string text, Color? text_color = null)
    {
        var options = new TextOptions(font)
        {
            FallbackFontFamilies = new[]
            {
                Fonts.GetEmojiFamily()
            }
        };
        var rect = TextMeasurer.MeasureAdvance(text, options);

        var c_w = Math.Ceiling(rect.Width);
        // magic number because imagesharp broke the MeasureSize method.
        var c_h = Math.Ceiling(rect.Height + 7);

        var text_texture = new Image<Rgba32>((int)Math.Max(c_w, 1), (int)Math.Max(c_h, 1), Color.Transparent);
        var text_point = PointF.Empty;

        text_color ??= Color.White;

        var cast_color = text_color.Value;

        text_texture.Mutate(x =>
            x.DrawText(text, font, Color.Black, text_point)
                .GaussianBlur(1f)
                .DrawText(text, font, cast_color, text_point)
        );

        var fs = (int)font.Size - 2;
        var fs_2 = font.Size * 0.5f;

        var circle_texture = new Image<Rgba32>(fs, fs, circle_color);
        circle_texture.Mutate(x =>
            x.ApplyRoundedCorners(fs_2));

        texture_data = new Image<Rgba32>(circle_texture.Width + 5 + text_texture.Width,
            text_texture.Height, Color.Transparent);
        texture_data.Mutate(x => x
            .DrawImage(circle_texture, new Point(1, 1), 1f)
            .Brightness(0.2f)
            .BoxBlur(2)
            .DrawImage(circle_texture, new Point(1, 1), 1f)
            .DrawImage(text_texture, new Point(circle_texture.Width + 6, 0), 1f)
        );

        Width = texture_data.Width;
        Height = texture_data.Height;
    }

    public Texture(IReadOnlyCollection<Texture> textures, float gap_px, float scale)
    {
        var texture_count = textures.Count;
        var size_w = (int)Math.Ceiling(15 * scale);
        const int max_images_per_line = 3;

        if (texture_count < 1)
        {
            texture_data = Transparent1x1.texture_data;
            Width = Height = 1;
            return;
        }

        var width = texture_count <= max_images_per_line
            ? texture_count * (size_w + gap_px)
            : max_images_per_line * (size_w + gap_px);
        var height = (int)Math.Max(size_w,
            Math.Ceiling((float)texture_count / max_images_per_line) * (size_w + gap_px));
        var image = new Image<Rgba32>((int)width, height);

        var x = 0;
        var y = 0;

        var break_w = (int)(140 * scale);

        foreach (var t in textures)
        {
            var x1 = x;
            var y1 = y;

            var texture = t.GetData();
            if (texture == null) continue;

            var t_height = (int)(size_w / ((float)texture.Width / texture.Height));
            texture.Mutate(r => r.Resize(size_w, t_height));

            var offset = (size_w - t_height) / 2;

            image.Mutate(r =>
                r.DrawImage(texture, new Point(x1, y1 + offset), 1f)
            );

            x += (int)(size_w + gap_px);
            if (x + size_w <= width) continue;

            x = 0;
            y += (int)(size_w + gap_px);
            if (y > break_w) break;
        }

        texture_data = image;
        Width = image.Width;
        Height = image.Height;
    }

    public unsafe Texture(Span<byte> data, int width, int height)
    {
        Width = width;
        Height = height;
        _handle = GL.GenTexture();
        if (_handle == 0) throw new Exception("Unable to generate texture handle.");
        Bind();

        fixed (void* d = &data[0])
        {
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba,
                PixelType.UnsignedByte, new IntPtr(d));
        }
    }

    public static Texture Transparent1x1
    {
        get
        {
            if (_transparent1x1 != null) return _transparent1x1;
            Span<byte> bytes = stackalloc byte[4] { 0, 0, 0, 0 };
            _transparent1x1 = new Texture(bytes, 1, 1);

            return _transparent1x1;
        }
    }

    public void Dispose()
    {
        if (_handle.HasValue)
            GL.DeleteTexture(_handle.Value);
        GC.SuppressFinalize(this);
    }

    private unsafe void LoadImage()
    {
        if (texture_data is null) throw new Exception("No texture data available.");

        var img = texture_data;
        _handle = GL.GenTexture();
        
        if (_handle == 0) throw new Exception("Unable to generate texture handle.");
        Bind();

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, Width, Height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
                fixed (void* data = accessor.GetRowSpan(y))
                {
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, accessor.Width, 1, PixelFormat.Rgba,
                        PixelType.UnsignedByte, new IntPtr(data));
                }
        });

        texture_data.Dispose();
        texture_data = null;
    }

    public bool NeedsLoading()
    {
        return !_handle.HasValue;
    }

    public void LoadOpenGLTexture()
    {
        LoadImage();
        SetParameters();
    }

    /// <summary>
    ///     Get the texture if not yet loaded by OpenGL. Returns null otherwise.
    /// </summary>
    /// <returns>The texture data or null.</returns>
    public Image<Rgba32>? GetData()
    {
        return texture_data;
    }

    private static void SetParameters()
    {
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 8);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }

    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        if (!_handle.HasValue) return;
        GL.ActiveTexture(textureSlot);
        GL.BindTexture(TextureTarget.Texture2D, _handle.Value);
    }
}
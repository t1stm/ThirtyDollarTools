using System.Reflection;
using OpenTK.Graphics.OpenGL;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;

namespace ThirtyDollarVisualizer.Objects;

public class Texture : IDisposable
{
    private readonly int _handle;
    public int Width;
    public int Height;
    
    public Texture(string path)
    {
        _handle = GL.GenTexture();
        Bind();
        
        Stream source;

        if (File.Exists(path))
        {
            source = File.OpenRead(path);
        }
        else
        {
            var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(path);

            source = stream ?? throw new FileNotFoundException($"Unable to find texture \'{path}\' in assembly or real path.");
        }

        using (var img = Image.Load<Rgba32>(source))
        {
            LoadImage(img);
        }

        SetParameters();
        
        source.Dispose();
    }

    public Texture(Font font, string text, Color? color = null)
    {
        _handle = GL.GenTexture();
        Bind();

        var options = new TextOptions(font);
        var rect = TextMeasurer.MeasureAdvance(text, options);
        
        using Image<Rgba32> image = new((int) Math.Ceiling(rect.Width), (int) Math.Ceiling(rect.Height), Color.Transparent);

        var point = PointF.Empty;

        color ??= Color.White;
        
        image.Mutate(x => 
                x.DrawText(text, font, Color.Black, point)
                .GaussianBlur(1)
                .DrawText(text, font, (Color) color, point)
            );
        
        LoadImage(image);
        SetParameters();
    }

    public unsafe Texture(Span<byte> data, int width, int height)
    {
        Width = width;
        Height = height;
        _handle = GL.GenTexture();
        Bind();

        fixed (void* d = &data[0])
        {
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, new IntPtr(d));
        }
        
        SetParameters();
    }

    private unsafe void LoadImage(Image<Rgba32> img)
    {
        Width = img.Width;
        Height = img.Height;
            
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, img.Width, img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                fixed (void* data = accessor.GetRowSpan(y))
                {
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, accessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, new IntPtr(data));
                }
            }
        });
    }

    private static void SetParameters()
    {
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 8);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }

    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        GL.ActiveTexture(textureSlot);
        GL.BindTexture(TextureTarget.Texture2D, _handle);
    }

    public void Dispose()
    {
        GL.DeleteTexture(_handle);
        GC.SuppressFinalize(this);
    }
}
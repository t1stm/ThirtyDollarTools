using System.Reflection;
using OpenTK.Graphics.OpenGL;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ThirtyDollarVisualizer.Objects;

public class Texture : IDisposable
{
    private static Texture? _transparent1x1;

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

    private int? _handle;
    private Image<Rgba32>? texture_data;
    public int Width;
    public int Height;
    
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

            source = stream ?? throw new FileNotFoundException($"Unable to find texture \'{path}\' in assembly or real path.");
        }

        texture_data = Image.Load<Rgba32>(source);
        Width = texture_data.Width;
        Height = texture_data.Height;
        
        source.Dispose();
    }

    public Texture(Font font, string text, Color? color = null)
    {
        var options = new TextOptions(font);
        var rect = TextMeasurer.MeasureSize(text, options);

        var c_w = Math.Ceiling(rect.Width);
        var c_h = Math.Ceiling(rect.Height);
        
        texture_data = new Image<Rgba32>((int) Math.Max(c_w, 1), (int) Math.Max(c_h, 1), Color.Transparent);
        Width = texture_data.Width;
        Height = texture_data.Height;
        
        var point = PointF.Empty;

        color ??= Color.White;

        var cast_color = (Color) color;
        
        texture_data.Mutate(x => 
                x.DrawText(text, font, Color.Black, point)
                .GaussianBlur(1f)
                .DrawText(text, font, cast_color, point)
            );
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
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, new IntPtr(d));
        }
    }

    private unsafe void LoadImage()
    {
        if (texture_data is null) throw new Exception("No texture data available.");
        
        var img = texture_data;
        _handle = GL.GenTexture();
        
        Console.WriteLine($"[Texture Handler] Loading texture handle: {_handle}");
        if (_handle == 0) throw new Exception("Unable to generate texture handle."); 
        Bind();
            
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, Width, Height, 
            0, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

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
        
        texture_data.Dispose();
        texture_data = null;
    }

    public bool NeedsLoading() => !_handle.HasValue;

    public void LoadOpenGLTexture()
    {
        LoadImage();
        SetParameters();
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
        if (!_handle.HasValue) return;
        GL.ActiveTexture(textureSlot);
        GL.BindTexture(TextureTarget.Texture2D, _handle.Value);
    }

    public void Dispose()
    {
        if (_handle.HasValue)
            GL.DeleteTexture(_handle.Value);
        GC.SuppressFinalize(this);
    }
}
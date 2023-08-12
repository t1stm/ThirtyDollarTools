using Silk.NET.OpenGL;

namespace ThirtyDollarVisualizer;

public class Texture
{
    private readonly byte[] TextureData;
    private readonly uint Width;
    private readonly uint Height;
    private uint TextureId;

    private readonly GL Gl;
    
    public Texture(GL gl, string path)
    {
        Gl = gl;
        if (!File.Exists(path)) throw new FileNotFoundException(
            $"Texture file for path \'{path}\' not found. Working directory is \'{Directory.GetCurrentDirectory()}\'.");
        
        var image = Image.Load<Rgba32>(path);

        Width = (uint) image.Width;
        Height = (uint) image.Height;

        var data = TextureData = new byte[4 * image.Width * image.Height];

        image.Mutate(x => x.Flip(FlipMode.Vertical));
        image.CopyPixelDataTo(data);

        LoadTexture();

        image.Dispose();
        // Leave data to be collected by the garbage collector.
        TextureData = Array.Empty<byte>();
    }

    private unsafe void LoadTexture()
    {
        var texture = TextureId = Gl.GenTexture();
        Gl.BindTexture(TextureTarget.Texture2D, texture);
        
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToBorder);
        Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToBorder);

        fixed (byte* pointer = TextureData)
        {
            Gl.TexImage2D(GLEnum.Texture, 0, InternalFormat.Rgba, Width, Height, 0, 
                PixelFormat.Rgba, PixelType.UnsignedByte, pointer);
        }
        
        Unbind();
    }

    public void Bind(TextureUnit textureUnit = TextureUnit.Texture0)
    {
        Gl.ActiveTexture(textureUnit);
        Gl.BindTexture(TextureTarget.Texture2D, TextureId);
    }

    public void Unbind()
    {
        Gl.BindTexture(TextureTarget.Texture2D,0);
    }

    ~Texture()
    {
        Gl.DeleteTexture(TextureId);
    }
}
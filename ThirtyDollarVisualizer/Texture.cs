using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer;

public class Texture
{
    private readonly byte[] TextureData;
    private readonly int Width;
    private readonly int Height;
    private int TextureId;

    
    public Texture(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(
            $"Texture file for path \'{path}\' not found. Working directory is \'{Directory.GetCurrentDirectory()}\'.");
        
        var image = Image.Load<Rgba32>(path);

        Width = image.Width;
        Height = image.Height;

        var data = TextureData = new byte[4 * image.Width * image.Height];

        image.Mutate(x => x.Flip(FlipMode.Vertical));
        image.CopyPixelDataTo(data);

        LoadTexture();

        image.Dispose();
        // Leave data to be collected by the garbage collector.
        TextureData = Array.Empty<byte>();
    }

    private void LoadTexture()
    {
        var texture = TextureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texture);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Clamp);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, Width, Height, 0, 
            PixelFormat.Rgba, PixelType.UnsignedByte, TextureData);
        
        Unbind();
    }

    public void Bind(TextureUnit textureUnit = TextureUnit.Texture0)
    {
        GL.ActiveTexture(textureUnit);
        GL.BindTexture(TextureTarget.Texture2D, TextureId);
    }

    public static void Unbind()
    {
        GL.BindTexture(TextureTarget.Texture2D,0);
    }

    ~Texture()
    {
        GL.DeleteTexture(TextureId);
    }
}
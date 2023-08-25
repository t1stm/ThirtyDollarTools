

using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Objects;

public class Texture : IDisposable
{
    private readonly int _handle;

    public unsafe Texture(string path)
    {
        _handle = GL.GenTexture();
        Bind();

        using (var img = Image.Load<Rgba32>(path))
        {
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

        SetParameters();
    }

    public unsafe Texture(Span<byte> data, int width, int height)
    {
        _handle = GL.GenTexture();
        Bind();

        fixed (void* d = &data[0])
        {
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, new IntPtr(d));
            SetParameters();
        }
    }

    private static void SetParameters()
    {
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
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
    }
}
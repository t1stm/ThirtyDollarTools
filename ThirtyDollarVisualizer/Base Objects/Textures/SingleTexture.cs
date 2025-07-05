using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ThirtyDollarVisualizer.Base_Objects.Textures;

public abstract class SingleTexture : IDisposable
{
    public int Width { get; protected set; }
    public int Height { get; protected set; }

    public abstract void Dispose();

    public abstract bool NeedsUploading();

    public virtual void Update()
    {
        // Override if needed.
    }

    public abstract void UploadToGPU(bool dispose);
    public virtual void UploadToGPU() => UploadToGPU(true);
    
    public abstract void Bind(TextureUnit slot = TextureUnit.Texture0);

    protected static void SetParameters()
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

    protected static unsafe void BasicUploadTexture(ImageFrame<Rgba32> image)
    {
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, image.Width, image.Height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)0);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
                fixed (void* data = accessor.GetRowSpan(y))
                {
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, accessor.Width, 1, PixelFormat.Rgba,
                        PixelType.UnsignedByte, new IntPtr(data));
                }
        });
    }

    protected static void BindPrimitive(int handle)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(handle, 1, nameof(handle));
        GL.BindTexture(TextureTarget.Texture2D, handle);
    }
}
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
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureBaseLevel, 0);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMaxLevel, 8);
        GL.GenerateMipmap(TextureTarget.Texture2d);
    }

    protected static unsafe void BasicUploadTexture(ImageFrame<Rgba32> image)
    {
        if (!image.DangerousTryGetSinglePixelMemory(out var memory))
        {
            throw new Exception("Could not get single pixel memory. " +
                                "Configuration is likely not passed as the backing memory is not contiguous.");
        }
        
        using var pin = memory.Pin();
        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba8, image.Width, image.Height,
            0, PixelFormat.Rgba, PixelType.UnsignedByte, pin.Pointer);
    }

    protected static void BindPrimitive(int handle)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(handle, 1, nameof(handle));
        GL.BindTexture(TextureTarget.Texture2d, handle);
    }
}
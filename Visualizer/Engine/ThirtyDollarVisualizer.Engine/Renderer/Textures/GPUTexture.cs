using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Debug;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;

namespace ThirtyDollarVisualizer.Engine.Renderer.Textures;

public class GPUTexture : IBindable
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public InternalFormat InternalFormat { get; set; } = InternalFormat.Rgba8;
    public MipmapMode MipmapMode { get; set; } = MipmapMode.Enabled;
    public int Handle { get; private set; }
    
    protected Queue<Action> UploadQueue { get; } = [];

    public BufferState BufferState { get; private set; } = BufferState.PendingCreation;

    public void Bind()
    {
        if (BufferState.HasFlag(BufferState.Failed))
            throw new Exception("Tried to bind a texture in a failed state.");

        if (BufferState.HasFlag(BufferState.PendingCreation))
            Create();

        GL.BindTexture(TextureTarget.Texture2d, Handle);
        if (BufferState.HasFlag(BufferState.PendingUpload))
            UploadToGPU();
    }

    public void Create()
    {
        Handle = GL.GenTexture();
        BufferState = (Handle > 0 ? BufferState.Created : BufferState.Failed) |
                      (BufferState ^ BufferState.PendingCreation);

        RenderMarker.Debug("Created Texture: ", $"({Handle})");
        UploadBlankTextureToGPU();
    }

    public void UploadBlankTextureToGPU()
    {
        if (BufferState.HasFlag(BufferState.Failed)) return;

        GL.BindTexture(TextureTarget.Texture2d, Handle);
        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat, Width, Height,
            0, PixelFormat.Rgba, PixelType.Byte, IntPtr.Zero);
        
        SetTexParams();
        RenderMarker.Debug(
            "Uploaded Blank Texture: ", $"({Handle.ToString()}) {Width}x{Height} InternalFormat: {InternalFormat}");
    }

    public unsafe void QueueUploadToGPU<TPixel>(ImageFrame<TPixel> frame, Rectangle? rect = null)
        where TPixel : unmanaged, IPixel, IPixel<TPixel>
    {
        lock (UploadQueue)
        {
            UploadQueue.Enqueue(() =>
            {
                if (!frame.DangerousTryGetSinglePixelMemory(out var pixelMemory))
                    throw new Exception("Unable to get pixel memory.");

                var handle = pixelMemory.Pin();
                var pixelInfo = UploadInfoProvider<TPixel>.UploadInfo;

                GL.TexSubImage2D(TextureTarget.Texture2d, 0, rect?.X ?? 0, rect?.Y ?? 0,
                    rect?.Width ?? Width, rect?.Height ?? Height, pixelInfo.Format, pixelInfo.Type, handle.Pointer);
                RenderMarker.Debug(
                    "Texture Upload: ",
                    $"({Handle.ToString()}) {rect?.ToString() ?? "Full"}, PixelFormat: {pixelInfo.Format}, PixelType: {pixelInfo.Type}");
            });
            BufferState |= BufferState.PendingUpload;
        }
    }

    protected void SetTexParams()
    {
        switch (MipmapMode)
        {
            case MipmapMode.Enabled:
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter,
                    (int)TextureMinFilter.LinearMipmapLinear);
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureBaseLevel, 0);
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMaxLevel, (int)Math.Floor(Math.Log2(Math.Max(Width, Height))));
                GL.GenerateMipmap(TextureTarget.Texture2d);
                break;
            
            case MipmapMode.Disabled:
            default:
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter,
                    (int)TextureMinFilter.Linear);
                
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureBaseLevel, 0);
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMaxLevel, 0);
                break;
        }
    }

    protected void UploadToGPU()
    {
        int count;
        lock (UploadQueue)
        {
            count = UploadQueue.Count;
            while (UploadQueue.TryDequeue(out var uploadAction)) uploadAction.Invoke();
        }
        
        if (count > 0) SetTexParams();
    }
}
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ThirtyDollarVisualizer.Objects.Textures.Animated;

public class AnimatedTexture(Image<Rgba32>? rgba) : AbstractTexture
{
    protected Image<Rgba32>? image = rgba;
    protected AnimatedHandle[]? gpu_handles;
    protected float TotalLength;
    protected readonly Stopwatch Stopwatch = new();
    
    public override bool NeedsUploading()
    {
        return gpu_handles == null;
    }

    public override void UploadToGPU()
    {
        if (image == null) throw new ArgumentNullException(nameof(image), "Animated Texture asset should not be null.");
        
        gpu_handles = new AnimatedHandle[image.Frames.Count];
        var handles = new int[image.Frames.Count];
        
        GL.GenTextures(image.Frames.Count, handles);
        var length = 0f;
        
        for (var index = 0; index < gpu_handles.Length; index++)
        {
            var handle = handles[index];
            var frame = image.Frames[index];
            
            var time = TryGetFrameDelay(frame);
            if (time == null)
                throw new Exception($"Frame {index} has no delay set.");
            
            length += time.Value;
            
            if (handle == 0)
                throw new Exception("Getting Animated Texture handles failed with one of them being null.");

            BindPrimitive(handle);
            BasicUploadTexture(frame);
            SetParameters();
            
            gpu_handles[index].Handle = handle;
            gpu_handles[index].Milliseconds = time.Value;
        }
        
        TotalLength = length;
        image.Dispose();
        image = null;
    }

    public override void Bind(TextureUnit slot = TextureUnit.Texture0)
    {
        if (gpu_handles == null) return;
        Stopwatch.Start();
        
        var elapsed = Stopwatch.ElapsedMilliseconds;
        var animation_window = elapsed % TotalLength;

        var texture_handle = gpu_handles[0];
        var current = 0f;
        
        foreach (var handle in gpu_handles.AsSpan())
        {
            current += handle.Milliseconds;
            if (animation_window < current) break;
            texture_handle = handle;
        }
        
        GL.ActiveTexture(slot);
        BindPrimitive(texture_handle.Handle);
    }

    private static float? TryGetFrameDelay(ImageFrame frame)
    {
        if (frame.Metadata.TryGetGifMetadata(out var gif))
            return gif.FrameDelay * 10f;
        
        if (frame.Metadata.TryGetPngMetadata(out var png))
            return png.FrameDelay.ToSingle() * 10f;

        if (frame.Metadata.TryGetWebpFrameMetadata(out var webp))
            return webp.FrameDelay;
        
        return null;
    }

    public override void Dispose()
    {
        if (gpu_handles == null)
        {
            GC.SuppressFinalize(this);
            return;
        }
        
        foreach (var frame in gpu_handles)
            GL.DeleteTexture(frame.Handle);
        
        GC.SuppressFinalize(this);
    }
}
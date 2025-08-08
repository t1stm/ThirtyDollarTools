using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Animated;

public class AnimatedTexture(Image<Rgba32>? rgba) : SingleTexture
{
    private readonly Stopwatch _stopwatch = new();
    private AnimatedHandle[]? _gpuHandles;
    private AnimatedHandle? _textureHandle;
    private float _totalLength;
    protected Image<Rgba32>? Image = rgba;
    public Image<Rgba32>? GetData() => Image;

    public override bool NeedsUploading()
    {
        return _gpuHandles == null;
    }

    public override void UploadToGPU(bool dispose)
    {
        if (Image == null) throw new InvalidOperationException("Animated Texture asset should not be null.");

        _gpuHandles = new AnimatedHandle[Image.Frames.Count];
        var handles = new int[Image.Frames.Count];

        GL.GenTextures(Image.Frames.Count, handles);
        var length = 0f;

        for (var index = 0; index < _gpuHandles.Length; index++)
        {
            var handle = handles[index];
            var frame = Image.Frames[index];

            var time = TryGetFrameDelay(frame);
            if (time == null)
                throw new Exception($"Frame {index} has no delay set.");

            length += time.Value;

            if (handle == 0)
                throw new Exception("Getting Animated Texture handles failed with one of them being null.");

            BindPrimitive(handle);
            BasicUploadTexture(frame);
            SetParameters();

            _gpuHandles[index].Handle = handle;
            _gpuHandles[index].Milliseconds = time.Value;
        }

        _totalLength = length;
        
        if (!dispose) return;
        
        Image.Dispose();
        Image = null;
    }

    public override void Update()
    {
        if (_gpuHandles == null) return;
        _stopwatch.Start();

        var elapsed = _stopwatch.ElapsedMilliseconds;
        var animation_window = elapsed % _totalLength;

        _textureHandle = _gpuHandles[0];
        var current = 0f;

        foreach (var handle in _gpuHandles.AsSpan())
        {
            current += handle.Milliseconds;
            _textureHandle = handle;
            if (animation_window < current) break;
        }
    }

    public override void Bind(TextureUnit slot = TextureUnit.Texture0)
    {
        if (_gpuHandles == null) return;
        var handle = _textureHandle ?? _gpuHandles[0];

        GL.ActiveTexture(slot);
        ArgumentOutOfRangeException.ThrowIfLessThan(handle.Handle, 1, nameof(handle));
        BindPrimitive(handle.Handle);
    }

    private static float? TryGetFrameDelay(ImageFrame frame)
    {
        if (frame.Metadata.TryGetGifMetadata(out var gif))
            return gif.FrameDelay * 10f;

        if (frame.Metadata.TryGetPngMetadata(out var png))
            return png.FrameDelay.ToSingle() * 100f;

        if (frame.Metadata.TryGetWebpFrameMetadata(out var webp))
            return webp.FrameDelay;

        return null;
    }

    public override void Dispose()
    {
        if (_gpuHandles == null)
        {
            GC.SuppressFinalize(this);
            return;
        }

        foreach (var frame in _gpuHandles)
            GL.DeleteTexture(frame.Handle);

        GC.SuppressFinalize(this);
    }
}
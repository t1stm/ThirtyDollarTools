using ManagedBass;

namespace Shared.Audio.BASS;

/// <summary>
///     A BASS push stream (<c>STREAMPROC_PUSH</c>). Measured on BASS 2.4 with
///     <c>TruePlayPosition = 0</c>: the queue <c>BASS_StreamPutData</c> reports leaves out the
///     ~128 ms already moved to the playback buffer, so <see cref="QueuedFrames" /> adds that back;
///     <c>BASS_ChannelSetPosition(0)</c> drops both and zeroes the position, playing or paused; a
///     stream that runs dry stalls with its position on the last frame and plays again on the
///     next put. Puts never block.
/// </summary>
public sealed class BassStream : AudioStream
{
    private readonly int _bytesPerFrame;
    private readonly int _handle;
    private readonly Lock _lock = new();
    private long _base;

    public BassStream(int sampleRate, int channels)
    {
        _bytesPerFrame = channels * sizeof(float);
        _handle = Bass.CreateStream(sampleRate, channels, BassFlags.Float, StreamProcedureType.Push);
    }

    public override long QueuedFrames =>
        ((long)Bass.StreamPutData(_handle, IntPtr.Zero, 0) +
         Bass.ChannelGetData(_handle, IntPtr.Zero, (int)DataFlags.Available)) / _bytesPerFrame;

    public override long PlayedPosition
    {
        get
        {
            lock (_lock)
            {
                return _base + Bass.ChannelGetPosition(_handle) / _bytesPerFrame;
            }
        }
    }

    public override unsafe void Put(ReadOnlySpan<float> interleaved)
    {
        fixed (float* samples = interleaved)
        {
            Bass.StreamPutData(_handle, (IntPtr)samples, interleaved.Length * sizeof(float));
        }
    }

    public override void Flush(long basePosition)
    {
        lock (_lock)
        {
            Bass.ChannelSetPosition(_handle, 0);
            _base = basePosition;
        }
    }

    public override void Play()
    {
        Bass.ChannelPlay(_handle);
    }

    public override void SetPause(bool paused)
    {
        if (paused) Bass.ChannelPause(_handle);
        else Bass.ChannelPlay(_handle);
    }

    public override void SetVolume(float volume)
    {
        Bass.ChannelSetAttribute(_handle, ChannelAttribute.Volume, volume);
    }

    public override void Delete()
    {
        Bass.StreamFree(_handle);
    }
}

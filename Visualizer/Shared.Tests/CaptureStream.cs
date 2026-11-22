using Serilog.Core;
using Sundex.Engine.Audio;

namespace Shared.Tests;

/// <summary>
///     Keeps what it is put; never fills up, so one pump writes out the rest of the song. Its
///     playhead is wherever the test says, on top of the last flush.
/// </summary>
internal sealed class CaptureStream() : AudioStream(Silent, 48000, 2, int.MaxValue)
{
    public static readonly AudioDevice Silent = AudioDevice.Open(Logger.None, silent: true);

    private readonly List<float> _put = [];
    public long Base { get; private set; }
    public long Played { get; set; }

    public float[] Take()
    {
        var taken = _put.ToArray();
        _put.Clear();
        return taken;
    }

    protected override long GetQueued() => 0;
    protected override long GetPlayed() => Base + Played;

    protected override int PutCore(ReadOnlySpan<float> interleaved)
    {
        _put.AddRange(interleaved);
        return interleaved.Length / Channels;
    }

    protected override void FlushCore(long basePosition)
    {
        _put.Clear();
        Base = basePosition;
        Played = 0;
    }

    protected override void SetPaused(bool paused)
    {
    }

    protected override void SetVolume(float volume)
    {
    }

    protected override void SetRate(float rate)
    {
    }

    protected override void Free()
    {
    }
}

using System.Diagnostics;

namespace Shared.Audio.Null;

/// <summary>
///     Discards what it is put but plays it in real time all the same, so the playhead under
///     <c>--no-audio</c> moves like a device's would: it advances while playing, and stops where
///     the data runs out.
/// </summary>
public sealed class NullStream(int sampleRate, int channels) : AudioStream
{
    private readonly Lock _lock = new();
    private long _base;
    private long _last = Stopwatch.GetTimestamp();
    private double _played;
    private long _put;
    private bool _running;

    public override long QueuedFrames
    {
        get
        {
            lock (_lock)
            {
                Advance();
                return _put - (long)_played;
            }
        }
    }

    public override long PlayedPosition
    {
        get
        {
            lock (_lock)
            {
                Advance();
                return _base + (long)_played;
            }
        }
    }

    public override void Put(ReadOnlySpan<float> interleaved)
    {
        lock (_lock)
        {
            Advance();
            _put += interleaved.Length / channels;
        }
    }

    public override void Flush(long basePosition)
    {
        lock (_lock)
        {
            Advance();
            _base = basePosition;
            _put = 0;
            _played = 0;
        }
    }

    public override void Play()
    {
        SetPause(false);
    }

    public override void SetPause(bool paused)
    {
        lock (_lock)
        {
            Advance();
            _running = !paused;
        }
    }

    public override void SetVolume(float volume)
    {
    }

    public override void Delete()
    {
    }

    private void Advance()
    {
        var now = Stopwatch.GetTimestamp();
        if (_running) _played = Math.Min(_played + (double)(now - _last) * sampleRate / Stopwatch.Frequency, _put);
        _last = now;
    }
}

using System.Diagnostics;
using Shared.Audio.Features;
using ThirtyDollarConverter.Voices;

namespace Shared.Audio;

/// <summary>
///     Plays a <see cref="VoiceList" /> by mixing it a block at a time on a feeder thread of its
///     own and keeping an <see cref="AudioStream" /> topped up to <c>aheadSeconds</c> in front of
///     the playhead. The stream's queue is native memory the audio device reads without entering
///     managed code, so a garbage collection only pauses the feeder and the queue covers it.
///     <para>
///         A new list (an edit, a mute, a solo) is <see cref="Publish" />ed and takes over at the
///         playhead: the feeder drops what it had queued from the old list and mixes the new one
///         from there. Seeks work the same way. The clock is the stream's played position, so it
///         stops where the song ends. The feeder allocates nothing once it has seen the largest
///         list it will play.
///     </para>
/// </summary>
public sealed class VoicePlayback : IBufferStopwatch, IDisposable
{
    /// <summary>Frames mixed per pass of the feeder: 21 ms at 48 kHz.</summary>
    public const int Block = 1024;

    private volatile int _aheadFrames;
    private readonly VoiceCursor _cursor = new();
    private readonly int _sampleRate;
    private readonly AudioStream _stream;
    private readonly Thread? _thread;
    private readonly ManualResetEventSlim _wake = new(false);

    /// <summary>Published, not taken by the feeder yet.</summary>
    private VoiceList? _pending;

    /// <summary>Frame to seek to, -1 for none; the clock reports it until the feeder has flushed to it.</summary>
    private long _pendingSeek = -1;

    private volatile bool _stopping;

    // what the feeder has spent mixing, against how much audio that made - see Load
    private long _busyTicks;
    private int _busyFrames;
    private float _load;

    public VoicePlayback(AudioContext context, int sampleRate, double aheadSeconds)
        : this(context.CreateStream(sampleRate, 2), sampleRate, aheadSeconds, true)
    {
    }

    /// <summary>
    ///     Without <paramref name="startFeeder" />, nothing feeds the stream until something calls
    ///     <see cref="Pump" /> - the allocation test drives it on its own thread.
    /// </summary>
    internal VoicePlayback(AudioStream stream, int sampleRate, double aheadSeconds, bool startFeeder)
    {
        _stream = stream;
        _sampleRate = sampleRate;
        _aheadFrames = (int)(aheadSeconds * sampleRate);
        if (!startFeeder) return;

        _thread = new Thread(Feed) { IsBackground = true, Name = "Voice feeder", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    /// <summary>How far ahead of the playhead the feeder keeps the stream filled.</summary>
    public double AheadSeconds
    {
        get => (double)_aheadFrames / _sampleRate;
        set => _aheadFrames = (int)(value * _sampleRate);
    }

    /// <summary>
    ///     The feeder's CPU load: time spent mixing over the length of the audio it mixed, so
    ///     0.01 means a second of song costs 10 ms. Past 1 the stream can't be kept up. Measured
    ///     over every quarter second of mixed audio; holds its last value while nothing is mixed.
    /// </summary>
    public float Load => Volatile.Read(ref _load);

    public bool IsRunning { get; private set; }
    public TimeSpan Elapsed => TimeSpan.FromMilliseconds(ElapsedMilliseconds);
    public long ElapsedMilliseconds => GetTime_Milliseconds();

    public void Start()
    {
        Play();
    }

    public void Stop()
    {
        SetPause(true);
    }

    public void Restart()
    {
        SeekTime_Milliseconds(0);
        SetPause(false);
    }

    public void Reset()
    {
        SeekTime_Milliseconds(0);
        SetPause(true);
    }

    public void Seek(long milliseconds)
    {
        SeekTime_Milliseconds(milliseconds);
    }

    public void Dispose()
    {
        _stopping = true;
        _wake.Set();
        _thread?.Join();
        _stream.Delete();

        Interlocked.Exchange(ref _pending, null)?.Dispose();
        _cursor.Attach(null, 0)?.Dispose();
        _cursor.Dispose();
    }

    /// <summary>
    ///     Hands <paramref name="list" /> to the feeder, which plays it from the playhead on and
    ///     disposes it once it moves on to another. The caller gives up one reference: call
    ///     <see cref="VoiceList.Retain" /> first to keep using the list.
    /// </summary>
    public void Publish(VoiceList list)
    {
        // a list published over one the feeder never took is never read
        Interlocked.Exchange(ref _pending, list)?.Dispose();
        _wake.Set();
    }

    /// <summary>Plays from the start, as a freshly uploaded buffer does.</summary>
    public void Play()
    {
        SeekTime_Milliseconds(0);
        SetPause(false);
    }

    public void SetPause(bool paused)
    {
        _stream.SetPause(paused);
        IsRunning = !paused;
    }

    public long GetTime_Milliseconds()
    {
        var seek = Interlocked.Read(ref _pendingSeek);
        return (seek >= 0 ? seek : _stream.PlayedPosition) * 1000 / _sampleRate;
    }

    public void SeekTime_Milliseconds(long milliseconds)
    {
        Interlocked.Exchange(ref _pendingSeek, Math.Max(milliseconds, 0) * _sampleRate / 1000);
        _wake.Set();
    }

    public void SetVolume(float volume)
    {
        _stream.SetVolume(volume);
    }

    private void MeasureLoad(long ticks, int frames)
    {
        _busyTicks += ticks;
        _busyFrames += frames;
        if (_busyFrames < _sampleRate / 4) return;

        var busy = (double)_busyTicks / Stopwatch.Frequency;
        Volatile.Write(ref _load, (float)(busy * _sampleRate / _busyFrames));
        _busyTicks = 0;
        _busyFrames = 0;
    }

    private void Feed()
    {
        Span<float> left = stackalloc float[Block];
        Span<float> right = stackalloc float[Block];
        Span<float> interleaved = stackalloc float[Block * 2];

        while (!_stopping)
        {
            Pump(left, right, interleaved);
            _wake.Wait(10);
            _wake.Reset();
        }
    }

    /// <summary>
    ///     One pass of the feeder: takes a published list or a seek, then tops the queue up. The
    ///     spans are scratch of <see cref="Block" />, <see cref="Block" /> and 2 × <see cref="Block" /> floats.
    /// </summary>
    internal void Pump(Span<float> left, Span<float> right, Span<float> interleaved)
    {
        var list = Interlocked.Exchange(ref _pending, null);
        var seek = Interlocked.Read(ref _pendingSeek);

        // with nothing to play yet, a seek waits for the first list
        if (list != null || (seek >= 0 && _cursor.List != null))
        {
            var position = Math.Min(seek >= 0 ? seek : _stream.PlayedPosition, (list ?? _cursor.List)!.Length);

            if (list != null) _cursor.Attach(list, (int)position)?.Dispose();
            else _cursor.Seek((int)position);
            _stream.Flush(position);

            // a seek that arrived meanwhile stays pending for the next pass
            Interlocked.CompareExchange(ref _pendingSeek, -1, seek);
        }

        var voices = _cursor.List;
        if (voices == null) return;

        while (_stream.QueuedFrames < _aheadFrames)
        {
            var frames = Math.Min(Block, voices.Length - _cursor.Position);
            if (frames <= 0) return;

            var start = Stopwatch.GetTimestamp();
            _cursor.Render(left[..frames], right[..frames]);
            if (voices.Channels < 2) left[..frames].CopyTo(right);

            for (var i = 0; i < frames; i++)
            {
                interleaved[2 * i] = left[i];
                interleaved[2 * i + 1] = right[i];
            }

            _stream.Put(interleaved[..(frames * 2)]);
            MeasureLoad(Stopwatch.GetTimestamp() - start, frames);
        }
    }
}

using OpenTK.Audio.OpenAL;

namespace Shared.Audio.OpenAL;

/// <summary>
///     One OpenAL source playing a queue of buffers, one per <see cref="Put" />. Buffers the source
///     has finished are unqueued and reused, so a steady stream stops generating buffer ids once
///     the queue is as deep as it gets. Measured on OpenAL Soft: <c>AL_SAMPLE_OFFSET</c> counts
///     from the first buffer still queued, so the playhead is the frames unqueued so far plus that
///     offset; a source that plays its whole queue stops, and a stopped source reports every
///     buffer - including ones queued afterwards - as processed and replays them all on the next
///     play, so a stopped source is emptied and rewound before anything new is queued.
/// </summary>
public sealed class OpenALStream : AudioStream
{
    private readonly int _channels;
    private readonly Format _format;
    private readonly Lock _lock = new();
    private readonly int _sampleRate;
    private readonly int _source;
    private long _base;

    // the source's queue, oldest first: ids and their frame counts, as a ring
    private int[] _queuedFrames = new int[64];
    private int[] _queuedIds = new int[64];
    private int _queueCount;
    private int _queueHead;
    private long _queueTotal;

    private int[] _free = new int[64];
    private int _freeCount;
    private bool _playing;
    private long _unqueued;

    public OpenALStream(int sampleRate, int channels)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _format = channels == 1 ? Format.FormatMonoFloat32 : Format.FormatStereoFloat32;
        _source = AL.GenSource();
    }

    public override long QueuedFrames
    {
        get
        {
            lock (_lock)
            {
                Reclaim();
                return _queueTotal - Offset();
            }
        }
    }

    public override long PlayedPosition
    {
        get
        {
            lock (_lock)
            {
                Reclaim();
                return _base + _unqueued + Offset();
            }
        }
    }

    public override unsafe void Put(ReadOnlySpan<float> interleaved)
    {
        lock (_lock)
        {
            Reclaim();

            var id = _freeCount > 0 ? _free[--_freeCount] : AL.GenBuffer();
            fixed (float* samples = interleaved)
            {
                AL.BufferData(id, _format, samples, interleaved.Length * sizeof(float), _sampleRate);
            }

            var queued = (uint)id;
            AL.SourceQueueBuffers(_source, 1, &queued);
            Enqueue(id, interleaved.Length / _channels);

            if (_playing && State() != SourceState.Playing) AL.SourcePlay(_source);
        }
    }

    public override void Flush(long basePosition)
    {
        lock (_lock)
        {
            AL.SourceStop(_source);
            UnqueueAll();
            AL.SourceRewind(_source);
            _base = basePosition;
            _unqueued = 0;
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
            _playing = !paused;
            var state = State();
            if (paused && state == SourceState.Playing) AL.SourcePause(_source);
            else if (!paused && _queueCount > 0 && state != SourceState.Playing) AL.SourcePlay(_source);
        }
    }

    public override void SetVolume(float volume)
    {
        // MaxGain first: it caps Gain and defaults to 1 - see OpenALBuffer.SetVolume.
        AL.Sourcef(_source, SourcePNameF.MaxGain, Math.Max(1f, volume));
        AL.Sourcef(_source, SourcePNameF.Gain, volume);
    }

    public override void Delete()
    {
        lock (_lock)
        {
            AL.SourceStop(_source);
            UnqueueAll();
            AL.DeleteSource(_source);
            AL.DeleteBuffers(_freeCount, _free);
            _freeCount = 0;
        }
    }

    private SourceState State()
    {
        return (SourceState)AL.GetSourcei(_source, SourceGetPNameI.SourceState);
    }

    private long Offset()
    {
        return _queueCount > 0 ? AL.GetSourcei(_source, SourceGetPNameI.SampleOffset) : 0;
    }

    /// <summary>Unqueues the buffers the source has finished, counting them as played.</summary>
    private void Reclaim()
    {
        if (State() == SourceState.Stopped)
        {
            // ran dry: everything queued has played
            _unqueued += _queueTotal;
            UnqueueAll();
            AL.SourceRewind(_source);
            return;
        }

        var processed = AL.GetSourcei(_source, SourceGetPNameI.BuffersProcessed);
        for (var i = 0; i < processed; i++)
        {
            _unqueued += _queuedFrames[_queueHead];
            Release(Dequeue());
        }
    }

    /// <summary>Unqueues every buffer; the source has to be stopped, which marks them all processed.</summary>
    private void UnqueueAll()
    {
        while (_queueCount > 0) Release(Dequeue());
    }

    private unsafe int Dequeue()
    {
        var id = _queuedIds[_queueHead];
        _queueTotal -= _queuedFrames[_queueHead];
        _queueHead = (_queueHead + 1) % _queuedIds.Length;
        _queueCount--;

        var unqueued = id;
        AL.SourceUnqueueBuffers(_source, 1, &unqueued);
        return id;
    }

    private void Enqueue(int id, int frames)
    {
        if (_queueCount == _queuedIds.Length)
        {
            // ponytail: grows by doubling, so only the first fill to a new depth allocates
            var ids = new int[_queuedIds.Length * 2];
            var counts = new int[ids.Length];
            for (var i = 0; i < _queueCount; i++)
            {
                ids[i] = _queuedIds[(_queueHead + i) % _queuedIds.Length];
                counts[i] = _queuedFrames[(_queueHead + i) % _queuedIds.Length];
            }

            (_queuedIds, _queuedFrames, _queueHead) = (ids, counts, 0);
        }

        var tail = (_queueHead + _queueCount) % _queuedIds.Length;
        _queuedIds[tail] = id;
        _queuedFrames[tail] = frames;
        _queueCount++;
        _queueTotal += frames;
    }

    private void Release(int id)
    {
        if (_freeCount == _free.Length) Array.Resize(ref _free, _free.Length * 2);
        _free[_freeCount++] = id;
    }
}

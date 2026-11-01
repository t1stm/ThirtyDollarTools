using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Serilog;

namespace Shared.Audio.Gpu;

/// <summary>
///     Mixes a <see cref="GpuVoiceCursor" />'s blocks on the GPU, a frame's budget at a time. Its
///     <see cref="Pump" /> runs once per frame on the render thread, where the app's context is
///     current: it reads back the blocks whose fences have signalled, then dispatches requested
///     ones until the frame's budget is spent - splitting a dense block's voice chunks over frames
///     when it has to. Mixing shares the GPU with the frame's draws, so the budget is small and
///     grows only while blocks pile up.
///     <para>
///         Any failure - a kernel that won't compile, a GL error, a list too big for a buffer - is
///         logged once and hands every block back to the CPU for the rest of the session.
///     </para>
/// </summary>
public sealed class GpuVoiceMixer(ILogger logger) : IDisposable
{
    /// <summary>GPU time per frame while keeping up, and the most it grows to while blocks pile up.</summary>
    private const double BudgetMs = 2, MaxBudgetMs = 8;

    /// <summary>Voice upload per frame: tau's 140 MB goes up over ~9 frames rather than in one hitch.</summary>
    private const long UploadBytesPerFrame = 16 << 20;

    private const int Queries = 8;

    private readonly GLSync[] _fences = new GLSync[GpuVoiceCursor.SlotCount];
    private readonly GpuVoiceCursor.Slot?[] _inFlight = new GpuVoiceCursor.Slot?[GpuVoiceCursor.SlotCount];
    private readonly ILogger _logger = logger.ForContext<GpuVoiceMixer>();

    // one pair of timestamps around each frame's dispatches, and what they dispatched
    private readonly int[] _queries = new int[2 * Queries];
    private readonly long[] _queryVoiceFrames = new long[Queries];
    private readonly int[] _queryAudioFrames = new int[Queries];
    private readonly bool[] _queryPending = new bool[Queries];
    private int _nextQuery;

    private int _chunk, _chunks;
    private bool _begun;
    private GpuVoiceCursor? _cursor;
    private GpuVoiceCursor.Slot? _current;
    private bool _failed;
    private GpuVoiceKernels? _kernels;

    /// <summary>Running estimate of a voice-frame's GPU cost; starts pessimistic, the first measurements correct it.</summary>
    private double _nsPerVoiceFrame = 0.05;

    // measured GPU time against the audio it mixed - see MsPerSecond
    private double _busyNs;
    private long _busyFrames;
    private float _msPerSecond;

    /// <summary>
    ///     GPU milliseconds spent per second of audio mixed, measured over every quarter second of
    ///     audio: the GPU's counterpart of <see cref="VoicePlayback.Load" />.
    /// </summary>
    public float MsPerSecond => Volatile.Read(ref _msPerSecond);

    /// <summary>Blocks the CPU mixed because the GPU was late or gone.</summary>
    public int Takeovers => Volatile.Read(ref _cursor)?.Takeovers ?? 0;

    public void Dispose()
    {
        _kernels?.Dispose();
        _kernels = null;
        foreach (var query in _queries)
            if (query != 0)
                GL.DeleteQuery(query);
    }

    /// <summary>A cursor for the player's <see cref="VoicePlayback" />; this mixer serves the newest one. Any thread.</summary>
    public GpuVoiceCursor CreateCursor()
    {
        var cursor = new GpuVoiceCursor();
        if (_failed) cursor.Fail();
        Volatile.Write(ref _cursor, cursor);
        return cursor;
    }

    /// <summary>Render thread, once per frame.</summary>
    public void Pump()
    {
        var cursor = Volatile.Read(ref _cursor);
        if (_failed || cursor == null || cursor.IsDisposed) return;

        try
        {
            // whatever the last frame's draws left behind isn't ours to report
            while (GL.GetError() != ErrorCode.NoError)
            {
            }

            _kernels ??= Create();
            Collect(cursor);
            Measure();
            Dispatch(cursor, _kernels);

            var error = GL.GetError();
            if (error != ErrorCode.NoError) throw new InvalidOperationException($"GL error {error}.");
        }
        catch (Exception e)
        {
            _logger.Error(e, "The GPU voice mixer failed; the CPU mixes from here on");
            _failed = true;
            cursor.Fail();
        }
    }

    private GpuVoiceKernels Create()
    {
        var kernels = new GpuVoiceKernels();
        kernels.EnsureOutput(GpuVoiceCursor.SlotCount, VoicePlayback.MaxBlockFrames);
        for (var i = 0; i < _queries.Length; i++) _queries[i] = GL.GenQuery();
        _logger.Information("GPU voice mixer ready: storage buffers up to {Mb} MB", kernels.MaxBufferBytes >> 20);
        return kernels;
    }

    /// <summary>Reads back every block whose fence has signalled and hands it to the feeder.</summary>
    private void Collect(GpuVoiceCursor cursor)
    {
        for (var i = 0; i < _inFlight.Length; i++)
        {
            var slot = _inFlight[i];
            if (slot == null) continue;

            var status = GL.ClientWaitSync(_fences[i], 0, 0);
            if (status is not (SyncStatus.AlreadySignaled or SyncStatus.ConditionSatisfied)) continue;

            GL.DeleteSync(_fences[i]);
            _fences[i] = default;
            _inFlight[i] = null;
            if (!GpuVoiceCursor.IsAbandoned(slot)) _kernels!.ReadOutput(slot.Index, slot.Result.AsSpan(0, 2 * slot.Frames));
            cursor.Complete(slot);
        }
    }

    /// <summary>Folds finished timestamp pairs into the cost estimate and <see cref="MsPerSecond" />.</summary>
    private void Measure()
    {
        for (var q = 0; q < Queries; q++)
        {
            if (!_queryPending[q]) continue;

            GL.GetQueryObjecti(_queries[2 * q + 1], QueryObjectParameterName.QueryResultAvailable, out var available);
            if (available == 0) continue;

            GL.GetQueryObjecti64(_queries[2 * q], QueryObjectParameterName.QueryResult, out var begin);
            GL.GetQueryObjecti64(_queries[2 * q + 1], QueryObjectParameterName.QueryResult, out var end);
            _queryPending[q] = false;

            var ns = (double)(end - begin);
            if (_queryVoiceFrames[q] > 0) _nsPerVoiceFrame = 0.8 * _nsPerVoiceFrame + 0.2 * (ns / _queryVoiceFrames[q]);

            _busyNs += ns;
            _busyFrames += _queryAudioFrames[q];
            if (_busyFrames < VoicePlayback.DefaultBlockFrames * 3) continue;

            Volatile.Write(ref _msPerSecond, (float)(_busyNs / 1e6 / (_busyFrames / 48000.0)));
            _busyNs = 0;
            _busyFrames = 0;
        }
    }

    /// <summary>Dispatches requested blocks, oldest first, until the frame's budget is spent.</summary>
    private void Dispatch(GpuVoiceCursor cursor, GpuVoiceKernels kernels)
    {
        var budget_ns = 1e6 * Math.Min(MaxBudgetMs, BudgetMs * (1 + cursor.Backlog() / 4.0));
        double spent_ns = 0;
        long voice_frames = 0;
        var audio_frames = 0;
        var query = -1;

        while (spent_ns < budget_ns)
        {
            if (_current == null)
            {
                _current = cursor.TakeNext();
                if (_current == null) break;

                _chunk = 0;
                _chunks = kernels.Chunks(_current.Count);
                _begun = false;
            }

            var slot = _current;
            if (GpuVoiceCursor.IsAbandoned(slot))
            {
                cursor.Complete(slot);
                _current = null;
                continue;
            }

            if (!ReferenceEquals(kernels.List, slot.List) && !kernels.SetList(slot.List!))
                throw new InvalidOperationException("The song's voices or sounds don't fit in a storage buffer.");

            // a big list goes up over frames; a block waits for the voices it reads
            var uploaded = kernels.UploadVoices(UploadBytesPerFrame);
            if (!uploaded && slot.Count > 0 && kernels.UploadedVoices <= slot.Active[slot.Count - 1]) break;

            if (query < 0) query = BeginQuery();
            if (!_begun)
            {
                kernels.BeginBlock(slot.Active.AsSpan(0, slot.Count), slot.Frames);
                _begun = true;
            }

            // a silent block has no chunks: pass 2 alone writes its zeros
            if (_chunk < _chunks)
            {
                var chunk_cost = _nsPerVoiceFrame * kernels.ChunkVoices * slot.Frames;
                var count = Math.Clamp((int)((budget_ns - spent_ns) / chunk_cost), 1, _chunks - _chunk);
                kernels.MixChunks(slot.Start, slot.Frames, slot.Count, _chunk, count);
                var voices = Math.Min((_chunk + count) * kernels.ChunkVoices, slot.Count) - _chunk * kernels.ChunkVoices;
                _chunk += count;
                spent_ns += count * chunk_cost;
                voice_frames += (long)voices * slot.Frames;
            }

            if (_chunk < _chunks) continue;

            kernels.Reduce(slot.Frames, _chunks, slot.Index);
            _fences[slot.Index] = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
            _inFlight[slot.Index] = slot;
            audio_frames += slot.Frames;
            _current = null;
        }

        if (query >= 0) EndQuery(query, voice_frames, audio_frames);
    }

    /// <summary>Starts this frame's timestamp pair, or returns a slot that won't be measured when all are in flight.</summary>
    private int BeginQuery()
    {
        var q = _nextQuery;
        if (_queryPending[q]) return Queries; // unmeasured: every pair is still in flight

        GL.QueryCounter(_queries[2 * q], QueryCounterTarget.Timestamp);
        return q;
    }

    private void EndQuery(int q, long voiceFrames, int audioFrames)
    {
        if (q == Queries) return;

        GL.QueryCounter(_queries[2 * q + 1], QueryCounterTarget.Timestamp);
        _queryVoiceFrames[q] = voiceFrames;
        _queryAudioFrames[q] = audioFrames;
        _queryPending[q] = true;
        _nextQuery = (q + 1) % Queries;
    }
}

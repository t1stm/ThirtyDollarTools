using System.Buffers;
using System.Diagnostics;
using ThirtyDollarConverter.Voices;

namespace Shared.Audio.Gpu;

/// <summary>
///     An <see cref="IVoiceCursor" /> whose blocks a <see cref="GpuVoiceMixer" /> mixes on the render
///     thread. The feeder asks for blocks ahead of the playhead into a ring of slots - each holding
///     the block's sounding voices, snapshotted by a <see cref="VoiceCursor" /> running ahead - and
///     <see cref="TryRender" /> hands them back in order once the GPU is done with them.
///     <para>
///         When the feeder is running dry (<c>force</c>) and the oldest block isn't back, the CPU
///         mixes it from its snapshot - at once if it is cheap, else once it has waited
///         <see cref="TakeoverAfterMs" />, since at a dense song's peak the CPU is far slower than a
///         frame or two of waiting. A seek or a new list drops every slot; one the render thread is
///         still working on is marked abandoned and freed by it later.
///     </para>
///     <para>
///         <see cref="IVoiceCursor" />'s members belong to the feeder; the internal ones marked
///         render thread belong to the mixer. Neither side allocates in steady state.
///     </para>
/// </summary>
public sealed class GpuVoiceCursor : IVoiceCursor
{
    /// <summary>Blocks in flight at most - also the mixer's output ring.</summary>
    public const int SlotCount = 16;

    /// <summary>How long a forced block waits for the GPU before the CPU mixes it.</summary>
    private const int TakeoverAfterMs = 100;

    /// <summary>A forced block with this few voices is mixed on the CPU at once: it costs less than the wait.</summary>
    private const int CheapVoices = 4096;

    private const int Free = 0, Requested = 1, Running = 2, Done = 3, Abandoned = 4;

    private readonly VoiceCursor _ahead = new();
    private readonly Slot[] _slots = new Slot[SlotCount];
    private volatile bool _failed;
    private volatile bool _disposed;

    /// <summary>Ring counters: the oldest block not handed back yet, and the next slot to fill. Feeder only.</summary>
    private int _head, _tail;

    private long _sequence;
    private int _takeovers;

    public GpuVoiceCursor()
    {
        for (var i = 0; i < SlotCount; i++) _slots[i] = new Slot(i);
    }

    /// <summary>
    ///     Off, every block is mixed on the feeder as <see cref="VoiceCursor" /> mixes it, and what
    ///     was in flight is dropped; on again, blocks go back to the GPU. Any thread.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    private volatile bool _enabled = true;

    /// <summary>Blocks the CPU mixed because the GPU was late or gone - for the debug overlay.</summary>
    public int Takeovers => Volatile.Read(ref _takeovers);

    internal bool IsDisposed => _disposed;

    public VoiceList? List => _ahead.List;
    public int Position { get; private set; }

    public VoiceList? Attach(VoiceList? list, int position)
    {
        DropAll();
        Position = position;
        return _ahead.Attach(list, position);
    }

    public void Seek(int position)
    {
        DropAll();
        Position = position;
        _ahead.Seek(position);
    }

    public bool TryRender(Span<float> left, Span<float> right, bool force)
    {
        var frames = left.Length;
        var list = _ahead.List;
        if (list == null || _failed || !_enabled) return RenderHere(left, right, list != null && _failed);

        Fill(list, frames);

        // nothing in flight: every slot is still out with the render thread after a seek
        if (_head == _tail) return force && RenderHere(left, right, true);

        var slot = _slots[_head % SlotCount];
        if (slot.Start != Position || slot.Frames != frames)
        {
            // the block size changed under the ring: start it over from the playhead
            Seek(Position);
            return force && RenderHere(left, right, true);
        }

        if (Volatile.Read(ref slot.State) == Done)
        {
            var result = slot.Result;
            for (var i = 0; i < frames; i++)
            {
                left[i] = result[2 * i];
                right[i] = result[2 * i + 1];
            }

            Hand(slot, frames);
            Fill(list, frames);
            return true;
        }

        if (!force) return false;
        if (slot.Count > CheapVoices && Stopwatch.GetElapsedTime(slot.RequestedAt).TotalMilliseconds < TakeoverAfterMs)
            return false;

        list.MixBlock(slot.Active.AsSpan(0, slot.Count), slot.Start, left, right);
        Interlocked.Increment(ref _takeovers);
        Hand(slot, frames);
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        DropAll();
        _ahead.Dispose();
        foreach (var slot in _slots)
        {
            // an abandoned slot's arrays are the render thread's until it lets go; the GC has them
            if (Volatile.Read(ref slot.State) != Free) continue;

            if (slot.Active.Length > 0) ArrayPool<int>.Shared.Return(slot.Active);
            slot.Active = [];
        }
    }

    /// <summary>
    ///     The GPU is gone for this session (it failed, or the list can't be uploaded): every block
    ///     from here is mixed on the feeder. Any thread.
    /// </summary>
    internal void Fail()
    {
        _failed = true;
    }

    /// <summary>Render thread: the oldest requested block, now <see cref="Running" /> and the caller's, or null.</summary>
    internal Slot? TakeNext()
    {
        while (true)
        {
            Slot? oldest = null;
            foreach (var slot in _slots)
                if (Volatile.Read(ref slot.State) == Requested && (oldest == null || slot.Sequence < oldest.Sequence))
                    oldest = slot;

            if (oldest == null) return null;
            if (Interlocked.CompareExchange(ref oldest.State, Running, Requested) == Requested) return oldest;
            // the feeder took it back meanwhile; look again
        }
    }

    /// <summary>Render thread: requested blocks nobody has taken yet - how far behind the GPU is.</summary>
    internal int Backlog()
    {
        var count = 0;
        foreach (var slot in _slots)
            if (Volatile.Read(ref slot.State) == Requested)
                count++;
        return count;
    }

    /// <summary>Render thread: whether the feeder dropped this slot while it was being mixed.</summary>
    internal static bool IsAbandoned(Slot slot)
    {
        return Volatile.Read(ref slot.State) == Abandoned;
    }

    /// <summary>
    ///     Render thread: lets go of a slot from <see cref="TakeNext" />, with
    ///     <see cref="Slot.Result" /> written. The feeder gets it, unless it abandoned the slot
    ///     meanwhile, in which case it is freed here.
    /// </summary>
    internal void Complete(Slot slot)
    {
        if (Interlocked.CompareExchange(ref slot.State, Done, Running) == Running) return;

        // abandoned: nobody wants the result
        slot.Release();
        Volatile.Write(ref slot.State, Free);
    }

    /// <summary>Requests blocks ahead until the ring is full or a slot is still out with the render thread.</summary>
    private void Fill(VoiceList list, int blockFrames)
    {
        while (_tail - _head < SlotCount)
        {
            var slot = _slots[_tail % SlotCount];
            if (Volatile.Read(ref slot.State) != Free) return;

            var start = _ahead.Position;
            var frames = Math.Min(blockFrames, list.Length - start);
            if (frames <= 0) return;

            var active = _ahead.Advance(frames);
            if (slot.Active.Length < active.Length)
            {
                if (slot.Active.Length > 0) ArrayPool<int>.Shared.Return(slot.Active);
                slot.Active = ArrayPool<int>.Shared.Rent(active.Length);
            }

            active.CopyTo(slot.Active);
            if (slot.Result.Length < 2 * frames) slot.Result = new float[2 * VoicePlayback.MaxBlockFrames];

            list.Retain();
            slot.List = list;
            slot.Start = start;
            slot.Frames = frames;
            slot.Count = active.Length;
            slot.Sequence = ++_sequence;
            slot.RequestedAt = Stopwatch.GetTimestamp();
            Volatile.Write(ref slot.State, Requested);
            _tail++;
        }
    }

    /// <summary>The block at the head is handed back: its slot goes free, the playhead moves on.</summary>
    private void Hand(Slot slot, int frames)
    {
        Drop(slot);
        _head++;
        Position += frames;
    }

    /// <summary>
    ///     Mixes the next block on this thread with the cursor that otherwise runs ahead - only right
    ///     when nothing is in flight, so it sits at the playhead.
    /// </summary>
    private bool RenderHere(Span<float> left, Span<float> right, bool takeover)
    {
        if (_head != _tail) Seek(Position);

        _ahead.Render(left, right);
        Position += left.Length;
        if (takeover) Interlocked.Increment(ref _takeovers);
        return true;
    }

    private void DropAll()
    {
        foreach (var slot in _slots) Drop(slot);
        _head = _tail;
    }

    /// <summary>Takes a slot back from wherever it is: freed now, or - while the render thread mixes it - abandoned to it.</summary>
    private static void Drop(Slot slot)
    {
        while (true)
        {
            var state = Volatile.Read(ref slot.State);
            switch (state)
            {
                case Free or Abandoned:
                    return;
                case Done:
                    slot.Release();
                    Volatile.Write(ref slot.State, Free);
                    return;
                case Requested when Interlocked.CompareExchange(ref slot.State, Free, Requested) == Requested:
                    slot.Release();
                    return;
                case Running when Interlocked.CompareExchange(ref slot.State, Abandoned, Running) == Running:
                    return;
            }
            // it moved on meanwhile (Requested -> Running, Running -> Done); look again
        }
    }

    /// <summary>
    ///     One block in flight. The feeder writes it while <see cref="Free" />; the render thread
    ///     reads it and writes <see cref="Result" /> while <see cref="Running" />.
    /// </summary>
    internal sealed class Slot(int index)
    {
        /// <summary>Position in the ring, and the mixer's output slot.</summary>
        public readonly int Index = index;

        /// <summary>The block's sounding voices, ascending, <see cref="Count" /> of them.</summary>
        public int[] Active = [];

        public int Count;
        public int Frames;

        /// <summary>The list the block is from, retained while the slot holds it.</summary>
        public VoiceList? List;

        public long RequestedAt;

        /// <summary>The mixed block, interleaved.</summary>
        public float[] Result = [];

        public long Sequence;
        public int Start;
        public int State;

        public void Release()
        {
            List?.Dispose();
            List = null;
        }
    }
}

using OpenTK.Mathematics;
using Shared;
using Shared.Renderer.Planes;
using Sundex.Core.Animations;
using ThirtyDollarConverter.Parser;
using VisualizerScene.Objects.Playfield.Batch;
using VisualizerScene.Objects.Playfield.Batch.Chunks;

namespace VisualizerScene.Objects.Playfield;

/// <summary>
///     One sequence, laid out and drawn. Only the chunks around the camera are built: the whole
///     sequence is laid out up front (<see cref="ChunkGenerator.LayOut" />, a few numbers per
///     chunk), and a pool of <see cref="PlayfieldChunk" />s is refilled by a thread of its own as
///     the camera moves. Memory follows what is on screen rather than the sequence's length -
///     a twelve-million-event sequence built whole took over 100 GB - and playing allocates
///     nothing: a refill reuses everything the chunk's last fill built. The pool only grows when
///     more chunks are wanted at once than ever before, as when zooming out.
/// </summary>
public class Playfield(PlayfieldSettings settings) : IDisposable
{
    /// <summary>Chunks kept built above the visible ones, for scrolling back a little.</summary>
    private const int ChunksAbove = 1;

    /// <summary>
    ///     Chunks built ahead below the visible ones - a screenful when that is more. Playback
    ///     scrolls down, so the next chunk has to be ready before it comes into view.
    /// </summary>
    private const int ChunksBelow = 4;

    // A slot's states. The render thread hands Free and Ready slots out to be filled (Queued);
    // the fill thread turns Queued into Ready, or back into Free when the camera has moved on.
    private const int Free = 0, Queued = 1, Ready = 2;

    private readonly ChunkGenerator _chunkGenerator = new(settings);

    /// <summary>The fill thread's own, since positioning a chunk moves the layout handler.</summary>
    private readonly ChunkGenerator _fillGenerator = new(settings);

    /// <summary>For <see cref="TryGetBounds" />, called from the player's thread.</summary>
    private readonly ChunkGenerator _lookupGenerator = new(settings);

    private readonly Lock _fillLock = new();
    private readonly Queue<Slot> _fillQueue = new();
    private readonly SemaphoreSlim _fillSignal = new(0);

    private readonly ColoredPlane _objectBox = new()
    {
        Color = (0, 0, 0, 0.25f)
    };

    private readonly Lock _renderLock = new();

    private readonly DollarStoreCamera _temporaryCamera = new(Vector3.Zero, Vector2i.Zero);
    private Vector3 _currentPosition = Vector3.Zero;
    private volatile bool _disposed;
    private BaseEvent[] _events = [];
    private Thread? _fillThread;

    private bool _firstPosition = true;
    private ChunkLayout[] _layouts = [];

    /// <summary>The slot each chunk is built (or queued) in, -1 for none. Written by the render thread only.</summary>
    private int[] _slotOf = [];

    /// <summary>Replaced when it grows, never changed in place: the player's thread reads it.</summary>
    private Slot[] _slots = [];

    private Vector3 _targetPosition = Vector3.Zero;

    // The chunks the camera wants built; the fill thread drops queued fills outside them.
    private int _wantedFirst, _wantedLast = -1;

    public bool DisplayCenter { get; set; } = true;

    /// <summary>The laid-out sequence's height, the box behind it included.</summary>
    public float Height { get; private set; }

    public void Dispose()
    {
        _disposed = true;
        _fillSignal.Release();

        lock (_renderLock)
        lock (_fillLock)
        {
            foreach (var slot in _slots) slot.Chunk.Dispose();
            _slots = [];
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Lays <paramref name="sequence" /> out. Called once, before the playfield is drawn.</summary>
    public void UpdateSounds(Sequence sequence)
    {
        var events = sequence.Events;
        var layouts = _chunkGenerator.LayOut(events);
        var handler = _chunkGenerator.LayoutHandler;

        var slotOf = new int[layouts.Length];
        Array.Fill(slotOf, -1);

        lock (_renderLock)
        {
            Height = handler.Height + handler.Size + handler.VerticalMargin;
            _events = events;
            _layouts = layouts;
            _slotOf = slotOf;
        }
    }

    public void Render(DollarStoreCamera realCamera, float zoom, double updateDelta)
    {
        var layoutWidth = _chunkGenerator.LayoutHandler.Width;
        var layoutHeight = Height;

        var leftMargin = (_temporaryCamera.Width - layoutWidth) / 2f;
        _targetPosition = DisplayCenter ? (-leftMargin, 0, 0) : Vector3.Zero;

        // avoid doing modifications to the main camera
        _temporaryCamera.CopyFrom(realCamera);
        if (_firstPosition)
        {
            _currentPosition = _targetPosition;
            _firstPosition = false;
        }

        _temporaryCamera.SetOffset(_currentPosition =
            SteppingFunctions.Exponential(_currentPosition, _targetPosition, updateDelta));
        _temporaryCamera.UpdateMatrix();

        // get generic camera values
        var camera_height = _temporaryCamera.Height;

        // set render culling limits
        var clamped_scale = Math.Min(zoom, 1f);
        var height_scale = camera_height / clamped_scale - camera_height;

        // get render culling camera values
        var camera_y = _temporaryCamera.Position.Y;
        var camera_yh = camera_y + camera_height;

        // fix values when the zoom is changed
        camera_y -= height_scale / 2;
        camera_yh += height_scale / 2;

        // position object box
        _objectBox.Position = (0, 0, 0);
        _objectBox.Scale = (layoutWidth, layoutHeight, 0);
        _objectBox.BorderRadius = 0f;

        _objectBox.UpdateModel(false);
        _objectBox.Render(_temporaryCamera);

        lock (_renderLock)
        {
            var layouts = _layouts;
            if (_disposed || layouts.Length == 0) return;

            var first = FirstChunkEndingBelow(layouts, camera_y);
            var last = first - 1;
            while (last + 1 < layouts.Length && layouts[last + 1].StartY <= camera_yh) last++;

            var anchor = Math.Min(first, layouts.Length - 1);
            var below = Math.Max(ChunksBelow, last - first + 1);
            Volatile.Write(ref _wantedFirst, Math.Max(0, anchor - ChunksAbove));
            Volatile.Write(ref _wantedLast, Math.Min(layouts.Length - 1, Math.Max(last, anchor) + below));

            // What is on screen is queued first, then what playback reaches next.
            for (var chunk = first; chunk <= last; chunk++) Request(chunk);
            for (var chunk = last + 1; chunk <= _wantedLast; chunk++) Request(chunk);
            for (var chunk = _wantedFirst; chunk < first; chunk++) Request(chunk);

            // A chunk still being filled is skipped this frame; it is a frame or two away.
            for (var chunk = first; chunk <= last; chunk++)
                if (_slotOf[chunk] is var id and >= 0 && Volatile.Read(ref _slots[id].State) == Ready)
                    _slots[id].Chunk.Render(_temporaryCamera);
        }
    }

    /// <summary>The first chunk whose bottom is at or below <paramref name="y" />; the chunks are in Y order.</summary>
    private static int FirstChunkEndingBelow(ChunkLayout[] layouts, float y)
    {
        int low = 0, high = layouts.Length;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (layouts[middle].EndY < y) low = middle + 1;
            else high = middle;
        }

        return low;
    }

    /// <summary>Has <paramref name="chunk" /> built, unless it is or is about to be.</summary>
    private void Request(int chunk)
    {
        Slot slot;
        if (_slotOf[chunk] is var id and >= 0)
        {
            slot = _slots[id];
            // Free here is a fill the thread dropped as unwanted before getting to it.
            if (Volatile.Read(ref slot.State) != Free) return;
        }
        else
        {
            slot = TakeSlot();
        }

        // Queued before it is remapped, so the player's thread never takes the old chunk's
        // renderables for the new one's.
        Volatile.Write(ref slot.State, Queued);
        if (slot.ChunkIndex >= 0 && _slotOf[slot.ChunkIndex] == slot.Id) _slotOf[slot.ChunkIndex] = -1;
        slot.ChunkIndex = chunk;
        _slotOf[chunk] = slot.Id;

        lock (_fillQueue)
        {
            _fillQueue.Enqueue(slot);
        }

        _fillSignal.Release();
        _fillThread ??= StartFillThread();
    }

    /// <summary>A slot to fill: an idle one, else the built one farthest from what is wanted, else a new one.</summary>
    private Slot TakeSlot()
    {
        Slot? farthest = null;
        var farthestDistance = 0;
        foreach (var slot in _slots)
        {
            var state = Volatile.Read(ref slot.State);
            if (state == Free) return slot;
            if (state != Ready) continue;

            var chunk = slot.ChunkIndex;
            var distance = chunk < _wantedFirst ? _wantedFirst - chunk : chunk - _wantedLast;
            if (distance <= farthestDistance) continue;

            farthest = slot;
            farthestDistance = distance;
        }

        if (farthest is not null) return farthest;

        // ponytail: grows one slot at a time, a copy of a few dozen references - only ever
        // when more chunks are wanted at once than before.
        var slots = new Slot[_slots.Length + 1];
        _slots.CopyTo(slots, 0);
        slots[^1] = new Slot(new PlayfieldChunk(settings), _slots.Length);
        Volatile.Write(ref _slots, slots);
        return slots[^1];
    }

    private Thread StartFillThread()
    {
        var thread = new Thread(FillLoop) { IsBackground = true, Name = "Playfield chunk fill" };
        thread.Start();
        return thread;
    }

    private void FillLoop()
    {
        while (true)
        {
            _fillSignal.Wait();
            if (_disposed) return;

            Slot slot;
            lock (_fillQueue)
            {
                slot = _fillQueue.Dequeue();
            }

            lock (_fillLock)
            {
                if (_disposed) return;

                // Dropped once the camera has moved on - after a seek, the chunks now on screen
                // mustn't wait behind the fills it left behind.
                var chunk = slot.ChunkIndex;
                if (chunk < Volatile.Read(ref _wantedFirst) || chunk > Volatile.Read(ref _wantedLast))
                {
                    Volatile.Write(ref slot.State, Free);
                    continue;
                }

                var size = _fillGenerator.ChunkSize;
                var start = chunk * size;
                var state = _layouts[chunk].State;
                _fillGenerator.LayoutHandler.SeekTo(state.SoundIndex, state.Y, state.Height);

                slot.Chunk.Fill(_events.AsSpan(start, Math.Min(_events.Length - start, size)),
                    _fillGenerator.LayoutHandler, settings);
                _fillGenerator.PositionChunk(slot.Chunk, state);

                Volatile.Write(ref slot.State, Ready);
            }
        }
    }

    /// <summary>The renderable of event <paramref name="index" /> while its chunk is built, else null.</summary>
    public SoundRenderable? GetRenderable(int index)
    {
        var slotOf = _slotOf;
        var slots = Volatile.Read(ref _slots);
        var size = _chunkGenerator.ChunkSize;
        var chunk = index / size;
        if (index < 0 || chunk >= slotOf.Length) return null;

        var id = slotOf[chunk];
        if (id < 0 || id >= slots.Length) return null;

        var slot = slots[id];
        if (Volatile.Read(ref slot.State) != Ready || slot.ChunkIndex != chunk) return null;

        var renderables = slot.Chunk.Renderables;
        var offset = index - chunk * size;
        return offset < renderables.Length ? renderables[offset] : null;
    }

    /// <summary>
    ///     Where event <paramref name="index" /> is drawn: its renderable while its chunk is
    ///     built, else the box the layout puts it in - so the camera can follow playback into
    ///     chunks that aren't built yet.
    /// </summary>
    public bool TryGetBounds(int index, out Vector3 position, out Vector3 scale)
    {
        if (GetRenderable(index) is { } renderable)
        {
            position = renderable.Position + renderable.Translation;
            scale = renderable.Scale;
            return true;
        }

        var events = _events;
        var layouts = _layouts;
        var chunk = index / _chunkGenerator.ChunkSize;
        if (index < 0 || index >= events.Length || chunk >= layouts.Length)
        {
            position = scale = Vector3.Zero;
            return false;
        }

        lock (_lookupGenerator)
        {
            var box = _lookupGenerator.BoxPosition(events, index, layouts[chunk].State);
            var size = _lookupGenerator.LayoutHandler.Size;
            position = (box.X, box.Y, 0);
            scale = (size, size, 0);
        }

        return true;
    }

    /// <summary>Stops every animation on the built chunks; the rest start still when they are built.</summary>
    public void ResetAnimations()
    {
        foreach (var slot in Volatile.Read(ref _slots))
        {
            if (Volatile.Read(ref slot.State) != Ready) continue;
            foreach (var renderable in slot.Chunk.Renderables) renderable.ResetAnimations();
        }
    }

    private sealed class Slot(PlayfieldChunk chunk, int id)
    {
        public readonly PlayfieldChunk Chunk = chunk;
        public readonly int Id = id;
        public int ChunkIndex = -1;
        public int State;
    }
}

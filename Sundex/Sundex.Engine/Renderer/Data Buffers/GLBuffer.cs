using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Enums;
using Sundex.Engine.Renderer.Queues;

namespace Sundex.Engine.Renderer.Data_Buffers;

/// <summary>
///     The GLBuffer class is responsible for managing OpenGL buffer objects. It supports
///     creating, binding, updating, and disposing of GPU buffers using the DeleteQueue.
/// </summary>
/// <typeparam name="TDataType">
///     The type of data stored in the buffer. It must be an unmanaged type.
/// </typeparam>
public class GLBuffer<TDataType>(DeleteQueue deleteQueue, BufferTarget bufferTarget)
    : IGPUBuffer<TDataType>, IIndexableCollection<int, TDataType> where TDataType : unmanaged
{
    private const int MaxPreallocatedUpdateCapacity = 32;

    private int? _newSize;
    private Dictionary<int, TDataType> Updates { get; } = new(MaxPreallocatedUpdateCapacity);
    public BufferState BufferState { get; protected set; } = BufferState.PendingCreation;
    public int Handle { get; protected set; }
    public int Capacity { get; protected set; }

    public void Bind()
    {
        if (BufferState.HasFlag(BufferState.PendingCreation))
            Create();

        if (BufferState.HasFlag(BufferState.Failed))
            throw new Exception("Tried to bind a buffer in a failed state.");

        GL.BindBuffer(bufferTarget, Handle);
    }

    public void Update()
    {
        UploadChangesIfAny();
    }

    public virtual unsafe void Dangerous_SetBufferData(ReadOnlySpan<TDataType> newData)
    {
        Bind();

        fixed (TDataType* ptr = newData)
        {
            GL.BufferData(bufferTarget, newData.Length * sizeof(TDataType), ptr, BufferUsage.StreamDraw);
        }

        Capacity = newData.Length;

        lock (Updates)
        {
            Updates.Clear();
        }
    }

    public TDataType this[int index]
    {
        get => ReadMemory(index);
        set => SetMemory(index, value);
    }

    public virtual void Dispose()
    {
        deleteQueue.Enqueue(DeleteType.VBO, Handle);
        GC.SuppressFinalize(this);
    }

    public int Count => Capacity;

    public void Create()
    {
        Handle = GL.GenBuffer();
        BufferState = Handle > 0 ? BufferState.Created : BufferState.Failed;
    }

    private unsafe void DangerousGLThread_Resize(int capacity)
    {
        Bind();
        GL.BufferData(bufferTarget, capacity * sizeof(TDataType), IntPtr.Zero, BufferUsage.StreamDraw);
        Capacity = capacity;
    }

    protected virtual unsafe void UploadChangesIfAny()
    {
        lock (Updates)
        {
            if (Updates.Count < 1) return;
            if (_newSize is not null)
            {
                DangerousGLThread_Resize(_newSize.Value);
                _newSize = null;
            }
            else
            {
                Bind();
            }

            var voidPtr = GL.MapBuffer(bufferTarget, BufferAccess.WriteOnly);
            var ptr = (TDataType*)voidPtr;
            Span<TDataType> data = new(ptr, Capacity);

            foreach (var (index, value) in Updates) data[index] = value;

            GL.UnmapBuffer(bufferTarget);
            Updates.Clear();
        }
    }

    protected virtual unsafe TDataType ReadMemory(int index)
    {
        if (Updates.TryGetValue(index, out var value))
            return value;

        Bind();
        var result = new TDataType();

        var ptr = (TDataType*)GL.MapBuffer(bufferTarget, BufferAccess.ReadOnly);
        var span = new Span<TDataType>(ptr, Capacity);
        var target = MemoryMarshal.CreateSpan(ref result, 1);

        span.CopyTo(target[index..(index + 1)]);
        GL.UnmapBuffer(bufferTarget);

        return result;
    }

    protected virtual void SetMemory(int index, TDataType value)
    {
        const float resizeMultiplier = 1.5f;

        lock (Updates)
        {
            Updates[index] = value;
            if (Capacity < index)
                _newSize = (int)(index * resizeMultiplier);
        }
    }

    public override string ToString()
    {
        return
            $"GLBuffer<{typeof(TDataType).Name}> [Handle: {Handle}, Target: {bufferTarget}, Capacity: {Capacity}, UpdatesCount: {Updates.Count}]";
    }

    /// <summary>
    ///     The GLBuffer.WithCPUCache extends the GLBuffer functionality by incorporating a CPU-side
    ///     buffer to mirror the GPU buffer's data. This enables efficient read and write operations
    ///     on the CPU while maintaining synchronization with the GPU buffer when necessary.
    ///     <para>
    ///         The CPU copy already holds every pending write, so instead of the base class's
    ///         per-index update map a write only widens the one range the next upload sends. A
    ///         map beside the copy doubled a buffer's memory until its first upload, and kept
    ///         its peak size after (a cleared dictionary keeps its capacity).
    ///     </para>
    ///     <seealso cref="GLBuffer&lt;TDataType&gt;" />
    /// </summary>
    public class WithCPUCache(DeleteQueue deleteQueue, BufferTarget bufferTarget)
        : GLBuffer<TDataType>(deleteQueue, bufferTarget)
    {
        private readonly BufferTarget _bufferTarget = bufferTarget;
        private readonly DeleteQueue _deleteQueue = deleteQueue;
        private readonly Lock _lock = new();

        // [_dirtyFrom, _dirtyTo): the elements written since the last upload.
        private int _dirtyFrom = int.MaxValue;
        private int _dirtyTo;

        /// <summary>The GPU buffer's size in elements; -1 until it has one. Differing from the copy's reallocates it.</summary>
        private int _gpuCapacity = -1;

        protected TDataType[] CPUBuffer { get; set; } = [];
        public ReadOnlySpan<TDataType> Data => CPUBuffer;

        public void ResizeCPUBuffer(int capacity)
        {
            lock (_lock)
            {
                Capacity = capacity;

                if (capacity < 1)
                    return;

                var oldBuffer = CPUBuffer.AsSpan();
                CPUBuffer = new TDataType[capacity];

                var copyLimit = Math.Min(oldBuffer.Length, Capacity);
                oldBuffer[..copyLimit].CopyTo(CPUBuffer);

                MarkDirty(0, capacity);
            }
        }

        protected override TDataType ReadMemory(int index)
        {
            lock (_lock)
            {
                return CPUBuffer[index];
            }
        }

        protected override void SetMemory(int index, TDataType value)
        {
            lock (_lock)
            {
                CPUBuffer[index] = value;
                MarkDirty(index, index + 1);
            }
        }

        private void MarkDirty(int from, int to)
        {
            _dirtyFrom = Math.Min(_dirtyFrom, from);
            _dirtyTo = Math.Max(_dirtyTo, to);
        }

        protected override unsafe void UploadChangesIfAny()
        {
            lock (_lock)
            {
                if (_dirtyFrom >= _dirtyTo) return;
                Bind();

                fixed (TDataType* ptr = CPUBuffer)
                {
                    if (_gpuCapacity != CPUBuffer.Length)
                    {
                        GL.BufferData(_bufferTarget, CPUBuffer.Length * sizeof(TDataType), ptr,
                            BufferUsage.StreamDraw);
                        _gpuCapacity = CPUBuffer.Length;
                    }
                    else
                    {
                        GL.BufferSubData(_bufferTarget, _dirtyFrom * sizeof(TDataType),
                            (_dirtyTo - _dirtyFrom) * sizeof(TDataType), ptr + _dirtyFrom);
                    }
                }

                _dirtyFrom = int.MaxValue;
                _dirtyTo = 0;
            }
        }

        public override void Dangerous_SetBufferData(ReadOnlySpan<TDataType> newData)
        {
            lock (_lock)
            {
                if (CPUBuffer.Length != newData.Length)
                    CPUBuffer = [.. newData];
                else newData.CopyTo(CPUBuffer);
                base.Dangerous_SetBufferData(newData);

                _gpuCapacity = newData.Length;
                _dirtyFrom = int.MaxValue;
                _dirtyTo = 0;
            }
        }

        public override void Dispose()
        {
            CPUBuffer = [];
            _deleteQueue.Enqueue(DeleteType.VBO, Handle);
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return
                $"GLBuffer<{typeof(TDataType).Name}>.WithCPUCache [Handle: {Handle}, Target: {_bufferTarget}, Capacity: {Capacity}, Dirty: {Math.Max(0, _dirtyTo - _dirtyFrom)}]";
        }
    }
}
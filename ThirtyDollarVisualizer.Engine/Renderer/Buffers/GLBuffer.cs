using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;
using ThirtyDollarVisualizer.Engine.Renderer.Queues;

namespace ThirtyDollarVisualizer.Engine.Renderer.Buffers;

/// <summary>
/// The GLBuffer class is responsible for managing OpenGL buffer objects. It supports
/// creating, binding, updating, and disposing of GPU buffers using the DeleteQueue.
/// </summary>
/// <typeparam name="TDataType">
/// The type of data stored in the buffer. It must be an unmanaged type.
/// </typeparam>
public class GLBuffer<TDataType>(DeleteQueue deleteQueue, BufferTarget bufferTarget)
    : IGPUBuffer<TDataType> where TDataType : unmanaged
{
    public BufferState BufferState { get; protected set; } = BufferState.PendingCreation;
    public int Handle { get; protected set; }
    public int Capacity { get; protected set; }
    protected Dictionary<int, TDataType> Updates { get; } = new();
    
    private int? _newSize;

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

    public void EnqueueNewData(ReadOnlySpan<TDataType> newData)
    {
        for (var i = 0; i < newData.Length; i++)
        {
            this[i] = newData[i];
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

    public void Create()
    {
        Handle = GL.GenBuffer();
        BufferState = Handle > 0 ? BufferState.Created : BufferState.Failed;
    }

    protected virtual unsafe void DangerousGLThread_Resize(int capacity)
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
            else Bind();
            
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
        return $"GLBuffer<{typeof(TDataType).Name}> [Handle: {Handle}, Target: {bufferTarget}, Capacity: {Capacity}, UpdatesCount: {Updates.Count}]";
    }

    /// <summary>
    /// The GLBuffer.WithCPUCache extends the GLBuffer functionality by incorporating a CPU-side
    /// buffer to mirror the GPU buffer's data. This enables efficient read and write operations
    /// on the CPU while maintaining synchronization with the GPU buffer when necessary.
    /// <seealso cref="GLBuffer&lt;TDataType&gt;"/>
    /// </summary>
    public class WithCPUCache(DeleteQueue deleteQueue, BufferTarget bufferTarget)
        : GLBuffer<TDataType>(deleteQueue, bufferTarget)
    {
        private readonly DeleteQueue _deleteQueue = deleteQueue;
        protected TDataType[] CPUBuffer { get; set; } = [];

        public ReadOnlySpan<TDataType> Data => CPUBuffer;

        protected override void DangerousGLThread_Resize(int capacity)
        {
            var copyEndIndex = Math.Min(CPUBuffer.Length, capacity);

            var newArray = new TDataType[capacity];
            var oldSpan = CPUBuffer.AsSpan()[..copyEndIndex];
            oldSpan.CopyTo(newArray);

            CPUBuffer = newArray;
            Capacity = newArray.Length;
            Dangerous_SetBufferData(CPUBuffer);
        }

        public void ResizeCPUBuffer(int capacity)
        {
            _newSize = capacity;
            Capacity = capacity;

            if (capacity < 1)
                return;

            var oldBuffer = CPUBuffer.AsSpan();
            CPUBuffer = new TDataType[capacity];

            var copyLimit = Math.Min(oldBuffer.Length, Capacity);
            oldBuffer[..copyLimit].CopyTo(CPUBuffer);

            EnqueueNewData(CPUBuffer);
        }

        protected override TDataType ReadMemory(int index)
        {
            lock (CPUBuffer)
                return CPUBuffer[index];
        }

        protected override void SetMemory(int index, TDataType value)
        {
            lock (CPUBuffer)
                CPUBuffer[index] = value;
            base.SetMemory(index, value);
        }

        public override void Dangerous_SetBufferData(ReadOnlySpan<TDataType> newData)
        {
            if (CPUBuffer.Length != newData.Length)
                CPUBuffer = newData.ToArray();
            else newData.CopyTo(CPUBuffer);
            base.Dangerous_SetBufferData(newData);
        }

        public override void Dispose()
        {
            CPUBuffer = [];
            _deleteQueue.Enqueue(DeleteType.VBO, Handle);
            GC.SuppressFinalize(this);
        }
        
        public override string ToString()
        {
            return $"GLBuffer<{typeof(TDataType).Name}>.WithCPUCache [Handle: {Handle}, Target: {bufferTarget}, Capacity: {Capacity}, UpdatesCount: {Updates.Count}]";
        }
    }
}
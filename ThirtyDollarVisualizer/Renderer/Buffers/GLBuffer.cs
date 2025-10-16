using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Enums;

namespace ThirtyDollarVisualizer.Renderer.Buffers;

public class GLBuffer<TDataType>(BufferTarget bufferTarget) : IGLBuffer<TDataType> where TDataType : unmanaged
{
    private int? _newSize;
    protected Dictionary<int, TDataType> Updates { get; } = new();
    public CreationState CreationState { get; protected set; } = CreationState.PendingCreation;
    public int Handle { get; protected set; }
    public int Capacity { get; protected set; }

    public void Bind()
    {
        if (CreationState.HasFlag(CreationState.PendingCreation))
            Create();

        if (CreationState.HasFlag(CreationState.Failed))
            throw new Exception("Tried to bind a buffer in a failed state.");

        GL.BindBuffer(bufferTarget, Handle);
    }

    public void Update()
    {
        UploadChangesIfAny();
    }

    public virtual unsafe void DangerousGLThread_SetBufferData(ReadOnlySpan<TDataType> newData)
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
        GL.DeleteBuffer(Handle);
        GC.SuppressFinalize(this);
    }

    public void Create()
    {
        Handle = GL.GenBuffer();
        CreationState = Handle > 0 ? CreationState.Created : CreationState.Failed;
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

            var ptr = (TDataType*)GL.MapBuffer(bufferTarget, BufferAccess.WriteOnly);
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
        lock (Updates)
        {
            Updates[index] = value;
            if (Capacity < index)
                _newSize = (int?)(index * 1.5);
        }
    }

    public class WithCPUCache(BufferTarget bufferTarget) : GLBuffer<TDataType>(bufferTarget)
    {
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
            DangerousGLThread_SetBufferData(CPUBuffer);
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

        public override void DangerousGLThread_SetBufferData(ReadOnlySpan<TDataType> newData)
        {
            if (CPUBuffer.Length != newData.Length)
                CPUBuffer = newData.ToArray();
            else newData.CopyTo(CPUBuffer);
            base.DangerousGLThread_SetBufferData(newData);
        }

        public override void Dispose()
        {
            CPUBuffer = [];
            GL.DeleteBuffer(Handle);
            GC.SuppressFinalize(this);
        }
    }
}
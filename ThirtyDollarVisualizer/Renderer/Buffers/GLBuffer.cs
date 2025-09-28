using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Enums;

namespace ThirtyDollarVisualizer.Renderer;

/// <summary>
///     Represents a GPU buffer for storing data, utilizing OpenGL for rendering purposes.
///     This is a generic class that supports unmanaged data types.
/// </summary>
/// <typeparam name="TDataType">The type of data stored in the buffer, which must be unmanaged.</typeparam>
public class GLBuffer<TDataType>(BufferTarget bufferTarget) : IGLBuffer<TDataType> where TDataType : unmanaged
{
    /// <summary>
    ///     Gets the dictionary tracking pending updates that have not yet been uploaded to the GPU.
    /// </summary>
    /// <value>A dictionary mapping indices to their pending update values.</value>
    protected Dictionary<int, TDataType> Updates { get; } = new();

    /// <summary>
    ///     Gets the current creation state of the buffer, indicating whether it's pending creation, created, or failed.
    /// </summary>
    /// <value>The current creation state of the buffer.</value>
    public CreationState CreationState { get; protected set; } = CreationState.PendingCreation;

    /// <summary>
    ///     Gets the OpenGL buffer handle identifier used for binding operations.
    /// </summary>
    /// <value>The OpenGL buffer handle, or 0 if not created.</value>
    public int Handle { get; protected set; }

    /// <summary>
    ///     Gets the current capacity of the buffer in number of elements.
    /// </summary>
    /// <value>The number of elements the buffer can hold.</value>
    public int Capacity { get; protected set; }

    /// <summary>
    ///     Binds the buffer to the current OpenGL context for use in subsequent operations.
    ///     Automatically creates the buffer if it hasn't been created yet.
    ///     Does nothing if buffer creation has failed.
    /// </summary>
    public void Bind()
    {
        if (CreationState.HasFlag(CreationState.PendingCreation)) 
            Create();

        if (CreationState.HasFlag(CreationState.Failed))
            throw new Exception("Tried to bind a buffer in a failed state.");

        GL.BindBuffer(bufferTarget, Handle);
    }

    /// <summary>
    ///     Uploads any pending changes to the GPU buffer.
    ///     This is a convenience method that calls UploadChangesIfAny().
    /// </summary>
    public void Update()
    {
        UploadChangesIfAny();
    }

    /// <summary>
    ///     Replaces the entire buffer's data with the given data.
    /// </summary>
    /// <param name="newData">The data to set to the buffer.</param>
    public virtual unsafe void SetBufferData(ReadOnlySpan<TDataType> newData)
    {
        Bind();

        fixed (TDataType* ptr = newData)
        {
            GL.BufferData(bufferTarget, newData.Length * sizeof(TDataType), ptr, BufferUsage.DynamicDraw);
        }
        Capacity = newData.Length;
        
        Updates.Clear();
    }

    /// <summary>
    ///     Gets the object at the given index from the GPU memory.
    /// </summary>
    /// <param name="index">The index of the object.</param>
    public TDataType this[int index]
    {
        get => ReadMemory(index);
        set => Updates[index] = value;
    }

    /// <summary>
    ///     Releases all resources used by the buffer by deleting the OpenGL buffer object.
    ///     Suppresses finalization to prevent the finalizer from running.
    /// </summary>
    public virtual void Dispose()
    {
        GL.DeleteBuffer(Handle);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Creates the OpenGL buffer object and assigns it a handle.
    ///     Updates the CreationState based on whether the creation was successful.
    /// </summary>
    public void Create()
    {
        Handle = GL.GenBuffer();
        CreationState = Handle > 0 ? CreationState.Created : CreationState.Failed;
    }

    /// <summary>
    ///     Resizes the GPU buffer to the specified capacity without preserving existing data.
    ///     The buffer is bound before resizing and uses dynamic draw usage pattern.
    /// </summary>
    /// <param name="capacity">The new capacity in number of elements.</param>
    protected virtual unsafe void Resize(int capacity)
    {
        Bind();
        GL.BufferData(bufferTarget, capacity * sizeof(TDataType), IntPtr.Zero, BufferUsage.DynamicDraw);
        Capacity = capacity;
    }

    /// <summary>
    ///     Uploads pending changes from the Updates dictionary to the GPU buffer using memory mapping.
    ///     Maps the GPU buffer for write access, applies all pending updates, then unmaps the buffer.
    ///     Clears the Updates dictionary after successful upload.
    /// </summary>
    protected virtual unsafe void UploadChangesIfAny()
    {
        if (Updates.Count < 1) return;

        Bind();
        var ptr = (TDataType*)GL.MapBuffer(bufferTarget, BufferAccess.WriteOnly);
        Span<TDataType> data = new(ptr, Capacity);

        foreach (var (index, value) in Updates) data[index] = value;

        GL.UnmapBuffer(bufferTarget);
        Updates.Clear();
    }

    /// <summary>
    ///     Reads GPU memory at the given index and returns an allocated object of type TDataType.
    /// </summary>
    /// <param name="index">The index of the wanted object.</param>
    /// <returns>Object of type TDataType at the given index.</returns>
    protected virtual unsafe TDataType ReadMemory(int index)
    {
        Bind();
        var result = new TDataType();

        var ptr = (TDataType*)GL.MapBuffer(bufferTarget, BufferAccess.ReadOnly);
        var span = new Span<TDataType>(ptr, 1);
        var target = MemoryMarshal.CreateSpan(ref result, 1);

        span.CopyTo(target);
        GL.UnmapBuffer(bufferTarget);

        return result;
    }

    /// <summary>
    ///     Represents a specialized GPU buffer with a CPU cache for more efficient data interaction and management.
    ///     This class extends the functionality of the base GLBuffer with support for maintaining a mirror of the GPU buffer
    ///     data in CPU memory.
    /// </summary>
    public class WithCPUCache(BufferTarget bufferTarget) : GLBuffer<TDataType>(bufferTarget)
    {
        /// <summary>
        ///     Gets or sets the CPU-side buffer that mirrors the GPU buffer data.
        /// </summary>
        protected TDataType[] CPUBuffer { get; set; } = [];

        /// <summary>
        ///     Gets a read-only span providing access to the cached CPU buffer data.
        /// </summary>
        /// <value>A read-only span of the cached buffer data.</value>
        public ReadOnlySpan<TDataType> Data => CPUBuffer;

        /// <summary>
        ///     Resizes both the GPU buffer and the CPU cache to the specified capacity.
        ///     Preserves existing data up to the smaller of the old or new capacity.
        /// </summary>
        /// <param name="capacity">The new capacity in number of elements.</param>
        protected override void Resize(int capacity)
        {
            base.Resize(capacity);
            var copyEndIndex = Math.Min(CPUBuffer.Length, capacity);

            var newArray = new TDataType[capacity];
            var oldSpan = CPUBuffer.AsSpan()[..copyEndIndex];
            oldSpan.CopyTo(newArray);

            CPUBuffer = newArray;
            base.SetBufferData(newArray);
        }

        /// <summary>
        ///     Reads data from the CPU cache at the specified index instead of accessing GPU memory.
        ///     This provides faster read access compared to the base implementation.
        /// </summary>
        /// <param name="index">The zero-based index of the element to read.</param>
        /// <returns>The element at the specified index from the CPU cache.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown when index is outside the bounds of the buffer.</exception>
        protected override TDataType ReadMemory(int index)
        {
            return CPUBuffer[index];
        }

        /// <summary>
        ///     Applies pending updates to both the CPU cache and GPU buffer.
        ///     Updates the CPU cache first, then calls the base implementation to sync with GPU.
        /// </summary>
        protected override void UploadChangesIfAny()
        {
            if (Updates.Count < 1) return;

            foreach (var (index, value) in Updates) CPUBuffer[index] = value;

            base.UploadChangesIfAny();
        }

        /// <summary>
        ///     Replaces the entire buffer's data in both CPU cache and GPU buffer with the specified data.
        /// </summary>
        /// <param name="newData">The new data to replace the buffer contents with.</param>
        public override void SetBufferData(ReadOnlySpan<TDataType> newData)
        {
            if (CPUBuffer.Length != newData.Length) 
                CPUBuffer = newData.ToArray();
            else newData.CopyTo(CPUBuffer);
            base.SetBufferData(newData);
        }

        /// <summary>
        ///     Releases all resources used by the buffer, including both CPU cache and GPU buffer.
        ///     Clears the CPU cache and deletes the OpenGL buffer object.
        /// </summary>
        public override void Dispose()
        {
            CPUBuffer = [];
            GL.DeleteBuffer(Handle);
            GC.SuppressFinalize(this);
        }
    }
}
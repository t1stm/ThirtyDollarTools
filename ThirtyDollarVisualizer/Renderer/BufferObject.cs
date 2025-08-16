using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Renderer;

public class BufferObject<TDataType>(int length, BufferTarget bufferType) : IDisposable, IBuffer where TDataType : unmanaged
{
    private readonly int _handle = GL.GenBuffer();
    private readonly Dictionary<int, TDataType> _updateQueue = new();

    /// <summary>
    /// Creates a new OpenGL buffer.
    /// </summary>
    /// <param name="data">The data the buffer should contain.</param>
    /// <param name="bufferType">The buffer's type.</param>
    /// <param name="drawHint">The buffer's draw hint.</param>
    public BufferObject(Span<TDataType> data, BufferTarget bufferType,
        BufferUsage drawHint = BufferUsage.StreamDraw) : this(data.Length, bufferType)
    {
        SetBufferData(data, drawHint);
    }

    /// <summary>
    /// A basic setter that allows queueing updates to the VBO.
    /// </summary>
    /// <param name="index">The index of the object you want to update at the next render pass.</param>
    public TDataType this[int index]
    {
        set => _updateQueue[index] = value;
    }

    /// <summary>
    /// The buffer's GL handle.
    /// </summary>
    public int Handle => _handle;

    /// <summary>
    /// An update method that checks for and applies any updates to the contents of the buffer.
    /// </summary>
    public unsafe void Update()
    {
        if (_updateQueue.Count < 1)
            return;

        Bind();
        // ooohhh, pointer casting in C#.
        // veri skeri
        var ptr = (TDataType*)GL.MapBuffer(bufferType, BufferAccess.WriteOnly);

        foreach (var (index, obj) in _updateQueue) ptr[index] = obj;

        GL.UnmapBuffer(bufferType);
        _updateQueue.Clear();
    }

    /// <summary>
    /// Binds the buffer object to its specified buffer target, making it the
    /// currently active buffer for further OpenGL operations.
    /// </summary>
    public void Bind()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Handle, 1, nameof(Handle));
        GL.BindBuffer(bufferType, _handle);
    }

    /// <summary>
    /// Releases all resources used by the buffer, including its OpenGL handle.
    /// </summary>
    public void Dispose()
    {
        GL.DeleteBuffer(_handle);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Sets the entire buffer's contents to the given data.
    /// </summary>
    /// <param name="data">The data to copy to the buffer.</param>
    /// <param name="drawHint">The draw hint.</param>
    public unsafe void SetBufferData(Span<TDataType> data, BufferUsage drawHint = BufferUsage.StreamDraw)
    {
        Bind();
        Manager.CheckErrors("BufferObject Bind");
        fixed (void* pointer = data)
        {
            GL.BufferData(bufferType, data.Length * sizeof(TDataType), new nint(pointer), drawHint);
        }
        Manager.CheckErrors("BufferObject Write");
    }

    /// <summary>
    /// Retrieves the count of elements stored in the buffer.
    /// </summary>
    /// <returns>The number of elements in the buffer.</returns>
    public int GetCount()
    {
        return length;
    }
}
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Renderer;

public class BufferObject<TDataType> : IDisposable, IBuffer where TDataType : unmanaged
{
    private readonly BufferTarget _bufferType;
    private readonly int _handle;
    private readonly int _length;
    private readonly Dictionary<int, TDataType> _updateQueue = new();

    /// <summary>
    ///     Creates a new OpenGL buffer.
    /// </summary>
    /// <param name="data">The data the buffer should contain.</param>
    /// <param name="bufferType">The buffer's type.</param>
    /// <param name="drawHint">The buffer's draw hint.</param>
    public BufferObject(Span<TDataType> data, BufferTarget bufferType,
        BufferUsageHint drawHint = BufferUsageHint.StreamDraw)
    {
        _bufferType = bufferType;
        _length = data.Length;

        GL.GenBuffers(1, out _handle);
        SetBufferData(data, bufferType, drawHint);
    }

    /// <summary>
    ///     A basic setter that allows queueing updates to the VBO.
    /// </summary>
    /// <param name="index">The index of the object you want to update at the next render pass.</param>
    public TDataType this[int index]
    {
        set => _updateQueue[index] = value;
    }

    /// <summary>
    ///     The buffer's GL handle.
    /// </summary>
    public int Handle => _handle;

    /// <summary>
    ///     An update method that checks for and applies any updates to the contents of the buffer.
    /// </summary>
    public unsafe void Update()
    {
        if (_updateQueue.Count < 1)
            return;

        Bind();
        // ooohhh, pointer casting in C#.
        // veri skeri
        var ptr = (TDataType*)GL.MapBuffer(_bufferType, BufferAccess.WriteOnly);

        foreach (var (index, obj) in _updateQueue) ptr[index] = obj;

        GL.UnmapBuffer(_bufferType);
        _updateQueue.Clear();
    }

    public void Bind()
    {
        GL.BindBuffer(_bufferType, _handle);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_handle);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Sets the entire buffer's contents to the given data.
    /// </summary>
    /// <param name="data">The data to copy to the buffer.</param>
    /// <param name="bufferType">The buffer type.</param>
    /// <param name="drawHint">The draw hint.</param>
    public unsafe void SetBufferData(Span<TDataType> data, BufferTarget bufferType,
        BufferUsageHint drawHint = BufferUsageHint.StreamDraw)
    {
        Bind();
        fixed (void* pointer = data)
        {
            GL.BufferData(bufferType, data.Length * sizeof(TDataType), new nint(pointer), drawHint);
        }
    }

    public int GetCount()
    {
        return _length;
    }
}
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Renderer;

public class GLBuffer<TDataType>(int length, BufferTarget bufferType, bool useCpuBuffer)
    : IDisposable, IBuffer where TDataType : unmanaged
{
    public int Length => length;
    public TDataType[]? CpuBuffer { get; private set; }
    
    private readonly Dictionary<int, TDataType> _updateQueue = new();

    /// <summary>
    /// Creates a new OpenGL buffer.
    /// </summary>
    /// <param name="data">The data the buffer should contain.</param>
    /// <param name="bufferType">The buffer's type.</param>
    /// <param name="useCpuBuffer">Whether to contain a copy of the buffer in CPU memory.</param>
    /// <param name="drawHint">The buffer's draw hint.</param>
    public GLBuffer(Span<TDataType> data, BufferTarget bufferType,
        BufferUsage drawHint = BufferUsage.StreamDraw, bool useCpuBuffer = false) : this(data.Length, bufferType,
        useCpuBuffer)
    {
        SetBufferData(data, drawHint);
    }

    /// <summary>
    /// A basic setter that allows queueing updates to the VBO.
    /// </summary>
    /// <param name="index">The index of the object you want to update at the next render pass.</param>
    public TDataType this[int index]
    {
        get => CpuBuffer?[index] ?? throw new Exception("Trying to read BufferObject when not using a CPU buffer.");
        set => _updateQueue[index] = value;
    }

    /// <summary>
    /// The buffer's GL handle.
    /// </summary>
    public int Handle { get; } = GL.GenBuffer();

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
        
        if (CpuBuffer != null)
            foreach (var (index, obj) in _updateQueue)
                CpuBuffer[index] = obj;
        _updateQueue.Clear();
    }

    /// <summary>
    /// Binds the buffer object to its specified buffer target, making it the
    /// currently active buffer for further OpenGL operations.
    /// </summary>
    public void Bind()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Handle, 1, nameof(Handle));
        GL.BindBuffer(bufferType, Handle);
    }

    /// <summary>
    /// Releases all resources used by the buffer, including its OpenGL handle.
    /// </summary>
    public void Dispose()
    {
        GL.DeleteBuffer(Handle);
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

        switch (useCpuBuffer)
        {
            case true when CpuBuffer == null:
                CpuBuffer = data.ToArray();
                break;

            case true when CpuBuffer != null:
            {
                for (var index = 0; index < data.Length; index++)
                {
                    var generic = data[index];
                    CpuBuffer[index] = generic;
                }

                break;
            }
        }

        Manager.CheckErrors("BufferObject Write");
    }
}
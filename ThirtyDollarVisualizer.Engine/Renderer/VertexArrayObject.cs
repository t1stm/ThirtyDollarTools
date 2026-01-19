using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Renderer.Buffers;
using ThirtyDollarVisualizer.Engine.Renderer.Debug;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;
using ThirtyDollarVisualizer.Engine.Renderer.Queues;

namespace ThirtyDollarVisualizer.Engine.Renderer;

/// <summary>
///     A class that represents a vertex array object.
/// </summary>
[PreloadGraphicsContext]
public class VertexArrayObject : IBindable, IGamePreloadable, IDisposable
{
    private static DeleteQueue _deleteQueue = null!;
    private readonly List<IBuffer> _buffers = [];
    private readonly Queue<(IBuffer, VertexBufferLayout)> _uploadQueue = [];
    private bool _disposed;
    private IBindable? _ibo;
    private bool _isIBOUploaded;
    private int _vertexIndex;
    public BufferState BufferState { get; private set; } = BufferState.PendingCreation;

    /// <summary>
    ///     The OpenGL handle of the vertex array object.
    /// </summary>
    public int Handle { get; private set; }

    /// <summary>
    ///     Binds the vertex array object to the OpenGL context.
    /// </summary>
    public void Bind()
    {
        if (BufferState.HasFlag(BufferState.Failed))
            throw new Exception("Tried to bind a failed VAO.");
        if (Handle < 1)
            Create();
        GL.BindVertexArray(Handle);
    }

    /// <summary>
    ///     Releases all resources used by the vertex array object.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        lock (_buffers)
        {
            _buffers.Clear();
        }

        _deleteQueue.Enqueue(DeleteType.VAO, Handle);
        GC.SuppressFinalize(this);
    }

    public static void Preload(AssetProvider assetProvider)
    {
        _deleteQueue = assetProvider.DeleteQueue;
    }

    private void Create()
    {
        Handle = GL.GenVertexArray();
        BufferState = Handle > 0 ? BufferState.Created : BufferState.Failed;

        RenderMarker.Debug("Vertex Array Object: ", $"({Handle}), State: {BufferState}");
    }

    /// <summary>
    ///     Adds a buffer to the vertex array object using the specified buffer object and vertex buffer layout.
    /// </summary>
    /// <param name="vbo">The buffer object to add to the vertex array object. <seealso cref="GLBuffer{TDataType}" /></param>
    /// <param name="layout">The layout describing how vertex attributes are organized within the buffer.</param>
    public void AddBuffer(IBuffer vbo, VertexBufferLayout layout)
    {
        lock (_uploadQueue)
        {
            _uploadQueue.Enqueue((vbo, layout));
        }
    }

    private void UploadBuffer(IBuffer vbo, VertexBufferLayout layout)
    {
        Bind();
        vbo.Bind();
        vbo.Update();

        _buffers.Add(vbo);

        var elements = layout.GetElements();
        var offset = 0;
        for (var i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            var vi = (uint)(_vertexIndex + i);

            GL.VertexAttribPointer(vi, el.Count, el.Type, el.Normalized, layout.GetStride(), new IntPtr(offset));
            GL.EnableVertexAttribArray(vi);

            offset += el.Count * el.Type.GetSize();
            if (el.Divisor != 0)
                GL.VertexAttribDivisor(vi, (uint)el.Divisor);

            RenderMarker.Debug("Uploaded Buffer to VAO: ", $"({Handle}), Params: [{vbo}, {layout}]");
        }

        _vertexIndex += elements.Count;
    }

    public void SetIndexBuffer(IBindable ibo)
    {
        _ibo = ibo;
    }

    /// <summary>
    ///     Binds the specified index buffer object to the vertex array object.
    /// </summary>
    /// <param name="ibo">The index buffer object to bind to the vertex array object.</param>
    private void BindIndexBuffer(IBindable ibo)
    {
        GL.VertexArrayElementBuffer(Handle, ibo.Handle);
        RenderMarker.Debug("Bound IBO Buffer to VAO: ", $"({Handle}), Params: ({ibo})");
    }

    private void UploadBufferLayouts()
    {
        lock (_uploadQueue)
        {
            while (_uploadQueue.Count != 0)
            {
                var (buffer, layout) = _uploadQueue.Dequeue();
                UploadBuffer(buffer, layout);
            }

            if (_ibo is null || _isIBOUploaded) return;
            BindIndexBuffer(_ibo);
            _isIBOUploaded = true;
        }
    }

    /// <summary>
    ///     Runs the update method for all buffers in the vertex array object.
    /// </summary>
    public void Update()
    {
        UploadBufferLayouts();
        lock (_buffers)
        {
            if (_disposed) return;
            foreach (var buffer in _buffers) buffer.Update();
        }
    }
}
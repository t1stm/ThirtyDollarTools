using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Buffers;

namespace ThirtyDollarVisualizer.Renderer;

/// <summary>
///     A class that represents a vertex array object.
/// </summary>
public class VertexArrayObject : IBindable
{
    private readonly List<IBuffer> _buffers = [];
    private readonly Queue<(IBuffer, VertexBufferLayout)> _uploadQueue = [];
    private int _vertexIndex;

    /// <summary>
    ///     The OpenGL handle of the vertex array object.
    /// </summary>
    public int Handle { get; private set; }

    private void Create()
    {
        Handle = GL.GenVertexArray();
    }
    
    /// <summary>
    ///     Binds the vertex array object to the OpenGL context.
    /// </summary>
    public void Bind()
    {
        if (Handle < 1)
            Create();
        GL.BindVertexArray(Handle);
    }

    /// <summary>
    ///     Adds a buffer to the vertex array object using the specified buffer object and vertex buffer layout.
    /// </summary>
    /// <param name="vbo">The buffer object to add to the vertex array object. <seealso cref="GLBuffer{TDataType}" /></param>
    /// <param name="layout">The layout describing how vertex attributes are organized within the buffer.</param>
    public void AddBuffer(IBuffer vbo, VertexBufferLayout layout)
    {
        lock (_uploadQueue)
            _uploadQueue.Enqueue((vbo, layout));
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
        }

        _vertexIndex += elements.Count;
    }

    /// <summary>
    ///     Binds the specified index buffer object to the vertex array object.
    /// </summary>
    /// <param name="ibo">The index buffer object to bind to the vertex array object.</param>
    public void BindIndexBuffer(IBindable ibo)
    {
        GL.VertexArrayElementBuffer(Handle, ibo.Handle);
    }

    private void UploadBufferLayouts()
    {
        lock (_uploadQueue)
            while (_uploadQueue.Count != 0)
            {
                var (buffer, layout) = _uploadQueue.Dequeue();
                UploadBuffer(buffer, layout);
            }
    }
    
    /// <summary>
    ///     Runs the update method for all buffers in the vertex array object.
    /// </summary>
    public void Update()
    {
        UploadBufferLayouts();
        foreach (var buffer in _buffers) buffer.Update();
    }

    /// <summary>
    ///     Releases all resources used by the vertex array object.
    /// </summary>
    public void Dispose()
    {
        _buffers.Clear();
        Manager.DeleteVertexArray(Handle);
    }
}
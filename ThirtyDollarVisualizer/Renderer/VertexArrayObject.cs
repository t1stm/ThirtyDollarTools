using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Renderer;

/// <summary>
/// A class that represents a vertex array object.
/// </summary>
public class VertexArrayObject : IBindable
{
    private readonly List<IBuffer> _buffers = [];
    private int _vertexIndex;

    /// <summary>
    /// Creates a new vertex array object and binds it to the OpenGL context.
    /// </summary>
    public VertexArrayObject()
    {
        Handle = GL.GenVertexArray();
        Bind();
    }

    /// <summary>
    /// The OpenGL handle of the vertex array object.
    /// </summary>
    public int Handle { get; }

    /// <summary>
    /// Binds the vertex array object to the OpenGL context.
    /// </summary>
    public void Bind()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Handle, 1, nameof(Handle));
        GL.BindVertexArray(Handle);
    }

    /// <summary>
    /// Adds a buffer to the vertex array object using the specified buffer object and vertex buffer layout.
    /// </summary>
    /// <param name="vbo">The buffer object to add to the vertex array object. <seealso cref="GLBuffer{TDataType}"/></param>
    /// <param name="layout">The layout describing how vertex attributes are organized within the buffer.</param>
    public void AddBuffer(IBuffer vbo, VertexBufferLayout layout)
    {
        Bind();
        vbo.Bind();
        _buffers.Add(vbo);

        var elements = layout.GetElements();
        var offset = 0;
        for (var i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            var vi = (uint)(_vertexIndex + i);

            GL.EnableVertexAttribArray(vi);
            GL.VertexAttribPointer(vi, el.Count, el.Type, el.Normalized, layout.GetStride(), new IntPtr(offset));
            offset += el.Count * el.Type.GetSize();
            if (el.Divisor != 0)
                GL.VertexAttribDivisor(vi, (uint)el.Divisor);
        }

        _vertexIndex += elements.Count;
    }

    /// <summary>
    /// Binds the specified index buffer object to the vertex array object.
    /// </summary>
    /// <param name="ibo">The index buffer object to bind to the vertex array object.</param>
    public void BindIndexBuffer(IBindable ibo)
    {
        GL.VertexArrayElementBuffer(Handle, ibo.Handle);
    }

    /// <summary>
    /// Runs the update method for all buffers in the vertex array object.
    /// </summary>
    public void Update()
    {
        foreach (var buffer in _buffers) buffer.Update();
    }

    /// <summary>
    /// Releases all resources used by the vertex array object.
    /// </summary>
    public void Dispose()
    {
        _buffers.Clear();
        GL.DeleteVertexArray(Handle);
    }
}
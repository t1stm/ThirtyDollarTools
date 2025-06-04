using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Renderer;

public class VertexArrayObject : IBindable
{
    private readonly List<IBuffer> _buffers = [];
    private int _vertexIndex;

    public VertexArrayObject()
    {
        Handle = GL.GenVertexArray();
        Bind();
    }

    public int Handle { get; }

    public void Bind()
    {
        GL.BindVertexArray(Handle);
    }

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
            var vi = _vertexIndex + i;

            GL.EnableVertexAttribArray(vi);
            GL.VertexAttribPointer(vi, el.Count, el.Type, el.Normalized, layout.GetStride(), offset);
            offset += el.Count * el.Type.GetSize();
            if (el.Divisor != 0)
                GL.VertexAttribDivisor(vi, el.Divisor);
        }

        _vertexIndex += elements.Count;
    }

    public void BindIndexBuffer(IBindable ibo)
    {
        GL.VertexArrayElementBuffer(Handle, ibo.Handle);
    }

    public void Update()
    {
        foreach (var buffer in _buffers) buffer.Update();
    }

    public void Dispose()
    {
        _buffers.Clear();
        GL.DeleteVertexArray(Handle);
    }
}
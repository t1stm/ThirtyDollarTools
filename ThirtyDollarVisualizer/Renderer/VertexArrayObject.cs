using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Renderer;

public class VertexArrayObject : IBindable 
{
    private readonly int _vao;

    public VertexArrayObject()
    {
        _vao = GL.GenVertexArray();
        Bind();
    }

    public void AddBuffer(IBuffer vbo, VertexBufferLayout layout)
    {
        Bind();
        vbo.Bind();
        var elements = layout.GetElements();
        var offset = 0;
        for (var i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            GL.EnableVertexAttribArray(i);
            GL.VertexAttribPointer(i, el.Count, el.Type, el.Normalized, layout.GetStride(), offset);
            offset += el.Count * el.Type.GetSize();
            if (el.Divisor != 0)
                GL.VertexAttribDivisor(i, el.Divisor);
        }
    }

    public void Bind()
    {
        GL.BindVertexArray(_vao);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
    }
}
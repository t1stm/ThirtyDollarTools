using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

public class VertexArrayObject<TVertexType> where TVertexType : unmanaged
{
    private readonly int _vao;

    public VertexArrayObject()
    {
        _vao = GL.GenVertexArray();
        Bind();
    }

    public void AddBuffer(BufferObject<TVertexType> vbo, VertexBufferLayout layout)
    {
        Bind();
        vbo.Bind();
        var elements = layout.GetElements();
        var offset = 0;
        for (uint i = 0; i < elements.Count; i++)
        {
            var el = elements[(int)i];
            GL.EnableVertexAttribArray(i);
            GL.VertexAttribPointer(i, el.Count, el.Type, el.Normalized, layout.GetStride(), offset);
            offset += el.Count * el.Type.GetSize();
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
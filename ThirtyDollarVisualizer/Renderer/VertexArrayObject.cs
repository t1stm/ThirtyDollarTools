using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Renderer;

public class VertexArrayObject : IBindable 
{
    private readonly List<IBuffer> buffers = [];
    private readonly int _vao;
    private int vertexIndex;

    public VertexArrayObject()
    {
        _vao = GL.GenVertexArray();
        Bind();
    }

    public void AddBuffer(IBuffer vbo, VertexBufferLayout layout)
    {
        Bind();
        vbo.Bind();
        buffers.Add(vbo);
        
        var elements = layout.GetElements();
        var offset = 0;
        for (var i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            var vi = vertexIndex + i;
            
            GL.EnableVertexAttribArray(vi);
            GL.VertexAttribPointer(vi, el.Count, el.Type, el.Normalized, layout.GetStride(), offset);
            offset += el.Count * el.Type.GetSize();
            if (el.Divisor != 0)
                GL.VertexAttribDivisor(vi, el.Divisor);
        }
        
        vertexIndex += elements.Count;
    }

    public void Bind()
    {
        GL.BindVertexArray(_vao);
    }

    public void Update()
    {
        foreach (var buffer in buffers)
        {
            buffer.Update();
        }
    }

    public void Dispose()
    {
        buffers.Clear();
        GL.DeleteVertexArray(_vao);
    }
}
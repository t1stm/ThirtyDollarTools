using Silk.NET.OpenGL;

namespace ThirtyDollarVisualizer;

public class VertexArray
{
    private readonly uint _vao;
    private readonly GL Gl;
    
    public VertexArray(GL gl)
    {
        Gl = gl;
        _vao = Gl.GenVertexArray();
        Gl.BindVertexArray(_vao);
    }

    public unsafe void AddBuffer(VertexBuffer vb, VertexBufferLayout layout)
    {
        Bind();
        vb.Bind();
        var elements = layout.GetElements();
        var offset = 0;
        for (uint i = 0; i < elements.Count; i++)
        {
            var el = elements[(int) i];
            Gl.EnableVertexAttribArray(i);
            Gl.VertexAttribPointer(i, el.Count, el.Type, el.Normalized, (uint) layout.GetStride(), (void*) offset);
            offset += el.Count * el.Type.GetSize();
        }
    }

    public void Bind()
    {
        Gl.BindVertexArray(_vao);
    }

    public void Unbind()
    {
        Gl.BindVertexArray(0);
    }

    ~VertexArray()
    {
        Gl.DeleteVertexArray(_vao);
    }
}
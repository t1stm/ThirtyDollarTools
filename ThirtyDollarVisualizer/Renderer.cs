using Silk.NET.OpenGL;

namespace ThirtyDollarVisualizer;

public class Renderer
{
    private readonly GL Gl; 
    public Renderer(GL gl)
    {
        Gl = gl;
    }
    
    public void Draw(VertexArray va, IndexBuffer ib, Shader shader)
    {
        shader.Bind();
        va.Bind();
        ib.Bind();
        Gl.DrawElements(PrimitiveType.Triangles, ib.GetCount(), DrawElementsType.UnsignedInt, 0);
    }
}
using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer
{
    public static class Renderer
    {
        public static void Draw(VertexArray<float> va, IndexBuffer ib, Shader shader)
        {
            shader.Bind();
            va.Bind();
            ib.Bind();
            GL.DrawElements(PrimitiveType.Triangles, ib.GetCount(), DrawElementsType.UnsignedInt, 0);
        }
    }
}
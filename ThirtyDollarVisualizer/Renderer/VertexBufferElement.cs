using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

public struct VertexBufferElement
{
    public int Count;
    public bool Normalized;
    public int Divisor;
    public VertexAttribPointerType Type;
}
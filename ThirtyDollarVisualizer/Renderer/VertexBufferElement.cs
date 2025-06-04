using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

/// <summary>
/// A definition of a vertex buffer element.
/// </summary>
public struct VertexBufferElement
{
    public int Count;
    public bool Normalized;
    public int Divisor;
    public VertexAttribPointerType Type;
}
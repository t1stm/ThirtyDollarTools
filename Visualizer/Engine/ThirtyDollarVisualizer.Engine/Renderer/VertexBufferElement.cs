using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Engine.Renderer;

/// <summary>
///     A definition of a vertex buffer element.
/// </summary>
public struct VertexBufferElement
{
    public int Count;
    public bool Normalized;
    public int Divisor;
    public VertexAttribPointerType Type;

    public override string ToString()
    {
        return $"VertexBufferElement (T: {Type} C: {Count}, N: {Normalized}, D: {Divisor})";
    }
}
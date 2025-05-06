using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

public static class VertexBufferExtensions
{
    public static int GetSize(this VertexAttribPointerType type)
    {
        return type switch
        {
            VertexAttribPointerType.Float => sizeof(float),
            VertexAttribPointerType.UnsignedInt => sizeof(uint),
            VertexAttribPointerType.UnsignedByte => sizeof(byte),
            _ => throw new NotSupportedException()
        };
    }
}
using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Engine.Renderer;

public static class VertexBufferExtensions
{
    /// <summary>
    /// Gets the size of a vertex attribute type.
    /// </summary>
    /// <param name="type">The vertex attribute pointer type.</param>
    /// <returns>An <see cref="int"/> with the size in bytes of the type.</returns>
    /// <exception cref="NotSupportedException">Thrown when the type isn't known.</exception>
    public static int GetSize(this VertexAttribPointerType type)
    {
        return type switch
        {
            VertexAttribPointerType.Float => sizeof(float),
            VertexAttribPointerType.UnsignedInt => sizeof(uint),
            VertexAttribPointerType.UnsignedByte => sizeof(byte),
            VertexAttribPointerType.Byte => sizeof(byte),
            VertexAttribPointerType.Short => sizeof(short),
            VertexAttribPointerType.UnsignedShort => sizeof(ushort),
            VertexAttribPointerType.Int => sizeof(int),
            VertexAttribPointerType.Double => sizeof(double),
            VertexAttribPointerType.HalfFloat => sizeof(float) / 2,
            VertexAttribPointerType.Fixed => sizeof(int),
            VertexAttribPointerType.Int64Arb => sizeof(long),
            VertexAttribPointerType.UnsignedInt64Arb => sizeof(ulong),
            VertexAttribPointerType.UnsignedInt2101010Rev => sizeof(int),
            VertexAttribPointerType.UnsignedInt10f11f11fRev => sizeof(int),
            VertexAttribPointerType.Int2101010Rev => sizeof(int),
            _ => throw new NotSupportedException()
        };
    }
}
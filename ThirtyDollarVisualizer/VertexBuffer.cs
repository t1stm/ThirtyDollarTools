using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer;

public class VertexBuffer<T> where T : struct
{
    private readonly uint _vbo;

    public VertexBuffer(T[] data)
    {
        var size = Marshal.SizeOf<T>();
        GL.GenBuffers(1, out _vbo);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * size, data, BufferUsageHint.StaticDraw);
    }

    public void Bind()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
    }

    public void Unbind()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    ~VertexBuffer()
    {
        GL.DeleteBuffer(_vbo);
    }
}
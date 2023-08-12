using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace ThirtyDollarVisualizer;

public class VertexBuffer
{
    private readonly uint _vbo;
    private readonly GL Gl;

    public unsafe VertexBuffer(GL gl, float[] data)
    {
        Gl = gl;
        var size = (long) Marshal.SizeOf<float>();
        Gl.GenBuffers(1, out _vbo);
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (void* pointer = data)
        {
            Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint) (data.LongLength * size), pointer, BufferUsageARB.StaticDraw);
        }
    }

    public void Bind()
    {
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    }

    public void Unbind()
    {
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    ~VertexBuffer()
    {
        Gl.DeleteBuffer(_vbo);
    }
}
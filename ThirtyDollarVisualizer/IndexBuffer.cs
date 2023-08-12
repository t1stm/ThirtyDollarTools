using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace ThirtyDollarVisualizer;

public class IndexBuffer
{
    private readonly uint _count;
    private readonly uint _ibo;

    private readonly GL Gl;

    public unsafe IndexBuffer(GL gl, uint[] data)
    {
        Gl = gl;
        
        _count = (uint) data.Length;
        Gl.GenBuffers(1, out _ibo);
        Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ibo);

        fixed (uint* pointer = data)
        {
            Gl.BufferData(BufferTargetARB.ElementArrayBuffer, _count * sizeof(uint), pointer, BufferUsageARB.StaticDraw);
        }
    }

    public void Bind()
    {
        Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ibo);
    }

    public void Unbind()
    {
        Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
    }

    public uint GetCount()
    {
        return _count;
    }

    ~IndexBuffer()
    {
        Gl.DeleteBuffer(_ibo);
    }
}
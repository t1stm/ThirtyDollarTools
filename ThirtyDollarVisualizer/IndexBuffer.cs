using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer;

public class IndexBuffer
{
    private readonly int _count;
    private readonly uint _ibo;

    public IndexBuffer(uint[] data)
    {
        _count = data.Length;
        GL.GenBuffers(1, out _ibo);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ibo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _count * sizeof(uint), data, BufferUsageHint.StaticDraw);
    }

    public void Bind()
    {
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ibo);
    }

    public void Unbind()
    {
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    public int GetCount()
    {
        return _count;
    }

    ~IndexBuffer()
    {
        GL.DeleteBuffer(_ibo);
    }
}
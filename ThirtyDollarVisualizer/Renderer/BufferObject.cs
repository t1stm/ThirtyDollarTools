
using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

public class BufferObject<TDataType> where TDataType : unmanaged
{
    private readonly uint _handle;
    private readonly BufferTarget _bufferType;
    private readonly int _length;

    public unsafe BufferObject(Span<TDataType> data, BufferTarget bufferType)
    {
        _bufferType = bufferType;
        _length = data.Length;
        
        GL.GenBuffers(1, out _handle);
        Bind();
        fixed (void* pointer = data)
        {
            GL.BufferData(bufferType, data.Length * sizeof(TDataType), new IntPtr(pointer), BufferUsageHint.StaticDraw);
        }
    }

    public int GetCount() => _length;

    public void Bind()
    {
        GL.BindBuffer(_bufferType, _handle);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_handle);
    }

    ~BufferObject()
    {
        Dispose();
    }
}
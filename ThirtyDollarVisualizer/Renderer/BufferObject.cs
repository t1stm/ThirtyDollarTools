using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

public class BufferObject<TDataType> : IDisposable where TDataType : unmanaged
{
    private readonly BufferTarget _bufferType;
    private readonly uint _handle;
    private readonly int _length;

    public uint Handle => _handle;

    public BufferObject(Span<TDataType> data, BufferTarget buffer_type, BufferUsageHint draw_hint = BufferUsageHint.DynamicDraw)
    {
        _bufferType = buffer_type;
        _length = data.Length;

        GL.GenBuffers(1, out _handle);
        SetBufferData(data, buffer_type, draw_hint);
    }

    public unsafe void SetBufferData(Span<TDataType> data, BufferTarget buffer_type, BufferUsageHint draw_hint = BufferUsageHint.DynamicDraw)
    {
        Bind();
        fixed (void* pointer = data)
        {
            GL.BufferData(buffer_type, data.Length * sizeof(TDataType), new IntPtr(pointer), draw_hint);
        }
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_handle);
        GC.SuppressFinalize(this);
    }

    public int GetCount()
    {
        return _length;
    }

    public void Bind()
    {
        GL.BindBuffer(_bufferType, _handle);
    }
}
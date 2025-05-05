using System.Collections.Concurrent;
using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

public class BufferObject<TDataType> : IDisposable where TDataType : unmanaged
{
    private readonly BufferTarget _bufferType;
    private readonly uint _handle;
    private readonly int _length;
    private readonly ConcurrentDictionary<int, TDataType> UpdateQueue = new();

    public BufferObject(Span<TDataType> data, BufferTarget buffer_type,
        BufferUsageHint draw_hint = BufferUsageHint.DynamicDraw)
    {
        _bufferType = buffer_type;
        _length = data.Length;

        GL.GenBuffers(1, out _handle);
        SetBufferData(data, buffer_type, draw_hint);
    }

    public uint Handle => _handle;

    public void Dispose()
    {
        GL.DeleteBuffer(_handle);
        GC.SuppressFinalize(this);
    }

    public unsafe void SetBufferData(Span<TDataType> data, BufferTarget buffer_type,
        BufferUsageHint draw_hint = BufferUsageHint.DynamicDraw)
    {
        Bind();
        fixed (void* pointer = data)
        {
            GL.BufferData(buffer_type, data.Length * sizeof(TDataType), new nint(pointer), draw_hint);
        }
    }

    public TDataType this[int index]
    {
        set => UpdateQueue[index] = value;
    }
    
    public unsafe void BindAndUpdate()
    {
        Bind();
        if (UpdateQueue.IsEmpty)
            return;
        
        var ptr = (TDataType*)GL.MapBuffer(_bufferType, BufferAccess.WriteOnly);
        
        foreach (var (index, obj) in UpdateQueue)
        {
            ptr[index] = obj;
        }
        
        GL.UnmapBuffer(_bufferType);
        UpdateQueue.Clear();
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
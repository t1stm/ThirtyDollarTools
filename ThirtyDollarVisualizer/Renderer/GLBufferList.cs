using System.Collections;
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Renderer;

public class GLBufferList<TDataType>(BufferTarget bufferTarget) : IList<TDataType>, IBuffer, IDisposable where TDataType : unmanaged
{
    public bool IsReadOnly => false;
    public int Handle => _bufferObject?.Handle ?? 0;

    public int Capacity => _bufferObject?.Length ?? 0;
    public int Count { get; private set; } = 0;
    
    private GLBuffer<TDataType>? _bufferObject;
    private GLBuffer<TDataType> GetOrCreateBuffer()
    {
        if (_bufferObject != null)
            return _bufferObject;

        var capacity = Math.Max(1, Capacity);
        return _bufferObject = new GLBuffer<TDataType>(capacity, bufferTarget, true);
    }

    public GLBuffer<TDataType> Buffer => GetOrCreateBuffer();

    private GLBuffer<TDataType> IncreaseCountOrExpandBuffer()
    {
        var bufferObject = GetOrCreateBuffer();
        if (++Count <= Capacity) return bufferObject;
        
        var capacity = Math.Max(Capacity * 2, 1);;
        var temporaryBuffer = new GLBuffer<TDataType>(capacity, bufferTarget, true);
        temporaryBuffer.SetBufferData(bufferObject.CpuBuffer ?? throw new Exception("Buffer is null"));
            
        bufferObject.Dispose();
        bufferObject = _bufferObject = temporaryBuffer;

        return bufferObject;
    }

    public void Add(TDataType data)
    {
        var bufferObject = IncreaseCountOrExpandBuffer();
        bufferObject[Count - 1] = data;
    }

    public void Clear()
    {
        var bufferObject = GetOrCreateBuffer();
        for (var i = 0; i < bufferObject.Length; i++)
        {
            bufferObject[i] = default;
        }
    }

    public bool Contains(TDataType item)
    {
        var bufferObject = GetOrCreateBuffer();
        var cpuBuffer = bufferObject.CpuBuffer;
        return cpuBuffer?.Contains(item) ?? false;
    }

    public void CopyTo(TDataType[] array, int arrayIndex)
    {
        var bufferObject = GetOrCreateBuffer();
        var cpuBuffer = bufferObject.CpuBuffer;
        cpuBuffer?.CopyTo(array, arrayIndex);
    }

    public bool Remove(TDataType item)
    {
        var bufferObject = GetOrCreateBuffer();
        var cpuBuffer = bufferObject.CpuBuffer;
        if (cpuBuffer == null) return false;
        
        Count = Math.Max(0, Count - 1);
        cpuBuffer[Count] = default;
        return true;
    }

    public void Bind() => _bufferObject?.Bind();
    public void Update() => _bufferObject?.Update();
    
    public void Dispose()
    {
        _bufferObject?.Dispose();
        _bufferObject = null;
        GC.SuppressFinalize(this);
    }

    public IEnumerator<TDataType> GetEnumerator()
    {
        var buffer = GetOrCreateBuffer();
        for (var i = 0; i < Count; i++)
        {
            yield return buffer[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int IndexOf(TDataType item)
    {
        var buffer = GetOrCreateBuffer();
        var cpuBuffer = buffer.CpuBuffer;
        if (cpuBuffer == null)
            return -1;

        for (var index = 0; index < cpuBuffer.Length; index++)
        {
            var temporary = cpuBuffer[index];
            if (temporary.Equals(item)) return index;
        }

        return -1;
    }

    public void Insert(int index, TDataType item)
    {
        var bufferObject = IncreaseCountOrExpandBuffer();
        if (index >= Count || index < 0)
            throw new IndexOutOfRangeException();
        
        // Shift elements to the right to make space at index
        for (var i = Count - 1; i > index; i--)
        {
            bufferObject[i] = bufferObject[i - 1];
        }
        
        // Place the new item
        bufferObject[index] = item;
    }

    public void RemoveAt(int index)
    {
        if (index >= Count || index < 0)
            throw new IndexOutOfRangeException();
        
        var bufferObject = GetOrCreateBuffer();
        
        // Shift elements left to fill the gap at index
        for (var i = index; i < Count - 1; i++)
        {
            bufferObject[i] = bufferObject[i + 1];
        }
        
        // Decrement count and clear the last logical item
        Count = Math.Max(0, Count - 1);
        bufferObject[Count] = default;
    }

    public TDataType this[int index]
    {
        get => GetOrCreateBuffer()[index];
        set => GetOrCreateBuffer()[index] = value;
    }
}
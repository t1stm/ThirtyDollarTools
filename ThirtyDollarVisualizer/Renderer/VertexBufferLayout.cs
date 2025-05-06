using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

public class VertexBufferLayout
{
    private readonly List<VertexBufferElement> _elements = [];
    private int _stride;

    public void PushFloat(int count)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.Float,
            Count = count,
            Normalized = false
        });
        _stride += sizeof(float) * count;
    }

    public void PushMatrix4(int count)
    {
        PushMatrix(4,4, count);
    }
    
    public void PushMatrix(int x, int y, int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x,4, nameof(x));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(y,4, nameof(y));
        
        for (var j = 0; j < count; j++)
        {
            for (var i = 0; i < y; i++)
            {
                _elements.Add(new VertexBufferElement {
                    Type       = VertexAttribPointerType.Float,
                    Count      = x,
                    Normalized = false,
                    Divisor    = 1
                });
            }
            _stride += sizeof(float) * x * y;
        }
    }

    public void PushUInt(int count)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.UnsignedInt,
            Count = count,
            Normalized = false
        });
        _stride += sizeof(uint) * count;
    }

    public void PushByte(int count)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.UnsignedByte,
            Count = count,
            Normalized = false
        });
        _stride += sizeof(byte) * count;
    }

    public IReadOnlyList<VertexBufferElement> GetElements()
    {
        return _elements;
    }

    public int GetStride()
    {
        return _stride;
    }
}
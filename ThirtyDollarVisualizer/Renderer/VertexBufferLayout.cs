using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

public class VertexBufferLayout
{
    private readonly List<VertexBufferElement> _elements = [];
    private int _stride;

    public VertexBufferLayout PushFloat(int count, bool per_instance = false)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.Float,
            Count = count,
            Normalized = false,
            Divisor = per_instance ? 1 : 0,
        });
        _stride += sizeof(float) * count;

        return this;
    }

    public VertexBufferLayout PushMatrix4(int count)
    {
        return PushMatrix(4, 4, count);
    }

    public VertexBufferLayout PushMatrix(int x, int y, int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x, 4, nameof(x));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(y, 4, nameof(y));

        for (var j = 0; j < count; j++)
        {
            for (var i = 0; i < y; i++)
            {
                _elements.Add(new VertexBufferElement
                {
                    Type = VertexAttribPointerType.Float,
                    Count = x,
                    Normalized = false,
                    Divisor = 1
                });
            }

            _stride += sizeof(float) * x * y;
        }

        return this;
    }

    public VertexBufferLayout PushUInt(int count, bool per_instance = false)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.UnsignedInt,
            Count = count,
            Normalized = false,
            Divisor = per_instance ? 1 : 0,
        });
        _stride += sizeof(uint) * count;

        return this;
    }

    public VertexBufferLayout PushByte(int count, bool per_instance = false)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.UnsignedByte,
            Count = count,
            Normalized = false,
            Divisor = per_instance ? 1 : 0,
        });
        _stride += sizeof(byte) * count;

        return this;
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
using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer;

public class VertexBufferLayout
{
    private readonly List<VertexBufferElement> _elements = new();
    private int _stride;

    public void PushFloat(int count)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.Float,
            Count = count,
            Normalized = true
        });
        _stride += sizeof(float) * count;
    }

    public void PushUInt(int count)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.UnsignedInt,
            Count = count,
            Normalized = true
        });
        _stride += sizeof(uint) * count;
    }

    public void PushByte(int count)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.UnsignedByte,
            Count = count,
            Normalized = true
        });
        _stride += sizeof(byte) * count;
    }

    public List<VertexBufferElement> GetElements()
    {
        return _elements;
    }

    public int GetStride()
    {
        return _stride;
    }
}
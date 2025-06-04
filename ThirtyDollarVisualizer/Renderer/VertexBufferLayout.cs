using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Renderer;

/// <summary>
/// A class representing how vertex attributes are organized within a vertex buffer.
/// </summary>
public class VertexBufferLayout
{
    private readonly List<VertexBufferElement> _elements = [];
    private int _stride;

    /// <summary>
    /// Adds a new <see cref="float"/> vertex attribute to the layout.
    /// </summary>
    /// <param name="count">The amount of <see cref="float"/> to add.</param>
    /// <param name="perInstance">Whether the value should be used per vertex or per instance.</param>
    /// <returns>The modified vertex buffer layout.</returns>
    public VertexBufferLayout PushFloat(int count, bool perInstance = false)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.Float,
            Count = count,
            Normalized = false,
            Divisor = perInstance ? 1 : 0
        });
        _stride += sizeof(float) * count;

        return this;
    }

    /// <summary>
    /// Adds a new <see cref="Matrix4"/> vertex attribute to the layout.
    /// </summary>
    /// <param name="count">The amount of <see cref="Matrix4"/> to add.</param>
    /// <returns>The modified vertex buffer layout.</returns>
    public VertexBufferLayout PushMatrix4(int count)
    {
        return PushMatrix(4, 4, count);
    }

    /// <summary>
    /// Adds a new custom matrix vertex attribute to the layout.
    /// </summary>
    /// <param name="count">The number of matrices to add.</param>
    /// <param name="x">The width of the matrix.</param>
    /// <param name="y">The height of the matrix.</param>
    /// <returns>The modified vertex buffer layout.</returns>
    public VertexBufferLayout PushMatrix(int x, int y, int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x, 4, nameof(x));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(y, 4, nameof(y));

        for (var j = 0; j < count; j++)
        {
            for (var i = 0; i < y; i++)
                _elements.Add(new VertexBufferElement
                {
                    Type = VertexAttribPointerType.Float,
                    Count = x,
                    Normalized = false,
                    Divisor = 1
                });

            _stride += sizeof(float) * x * y;
        }

        return this;
    }

    /// <summary>
    /// Adds a new <see cref="uint"/> vertex attribute to the layout.
    /// </summary>
    /// <param name="count">The amount of <see cref="uint"/> to add.</param>
    /// <param name="perInstance">Whether the value should be used per vertex or per instance.</param>
    /// <returns>The modified vertex buffer layout.</returns>
    public VertexBufferLayout PushUInt(int count, bool perInstance = false)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.UnsignedInt,
            Count = count,
            Normalized = false,
            Divisor = perInstance ? 1 : 0
        });
        _stride += sizeof(uint) * count;

        return this;
    }

    /// <summary>
    /// Adds a new <see cref="byte"/> vertex attribute to the layout.
    /// </summary>
    /// <param name="count">The amount of <see cref="byte"/> to add.</param>
    /// <param name="perInstance">Whether the value should be used per vertex or per instance.</param>
    /// <returns>The modified vertex buffer layout.</returns>
    public VertexBufferLayout PushByte(int count, bool perInstance = false)
    {
        _elements.Add(new VertexBufferElement
        {
            Type = VertexAttribPointerType.UnsignedByte,
            Count = count,
            Normalized = false,
            Divisor = perInstance ? 1 : 0
        });
        _stride += sizeof(byte) * count;

        return this;
    }

    /// <summary>
    /// Gets all elements in the layout.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<VertexBufferElement> GetElements()
    {
        return _elements;
    }

    /// <summary>
    /// Gets the stride of the layout.
    /// </summary>
    /// <returns>The current stride.</returns>
    public int GetStride()
    {
        return _stride;
    }
}
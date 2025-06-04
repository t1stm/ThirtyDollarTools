using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Renderer.Instanced;

/// <summary>
///     Represents a collection of quads that can only be allocated once.
///     When the size is changed due to user requirements, the allocation must happen with a new object.
/// </summary>
public class QuadArray : IDisposable
{
    private static BufferObject<float>? _quadVBO;
    private static BufferObject<uint> _quadEBO = null!;
    private readonly Quad[] _array;

    private readonly Shader _shader = ShaderPool.GetOrLoad("quad_shader", () =>
        Shader.NewVertexFragment("ThirtyDollarVisualizer.Assets.Shaders.quad.vert",
            "ThirtyDollarVisualizer.Assets.Shaders.quad.frag"));

    private VertexArrayObject _arrayVAO = null!;
    private BufferObject<Quad> _arrayVBO = null!;

    public QuadArray(int count) : this(new Quad[count])
    {
    }

    public QuadArray(Quad[] array)
    {
        if (array.Length < 1)
            throw new Exception("QuadArray must have at least one quad");

        _array = array;
        UploadToGPU();
    }

    public ref Quad this[int index] => ref _array[index];

    public void Dispose()
    {
        _arrayVAO.Dispose();
        _arrayVBO.Dispose();
        GC.SuppressFinalize(this);
    }

    protected void UploadToGPU()
    {
        if (_quadVBO == null)
            InitQuadVBO();

        _arrayVBO = new BufferObject<Quad>(_array, BufferTarget.ArrayBuffer);
        _arrayVAO = new VertexArrayObject();

        _quadEBO.Bind();
        _arrayVAO.AddBuffer(
            _quadVBO!,
            new VertexBufferLayout()
                .PushFloat(3) // aPosition
                .PushFloat(2) // aUV
        );

        _arrayVAO.AddBuffer(
            _arrayVBO,
            new VertexBufferLayout()
                .PushMatrix4(1) // iModel
                .PushFloat(4, true) // iColor
                .PushUInt(1, true) // iTextureIndex
        );

        _arrayVAO.BindIndexBuffer(_quadEBO);
    }

    public PointerPlane[] ToPointerPlanes()
    {
        return _array.Select((_, i) => new PointerPlane(this, i)).ToArray();
    }

    protected static void InitQuadVBO()
    {
        var (x, y, z) = (0f, 0f, 0);
        var (w, h) = (1f, 1f);

        Span<float> vertices =
        [
            // Position                     // Texture Coordinates
            x, y + h, z, 0.0f, 1.0f, // Bottom-left
            x + w, y + h, z, 1.0f, 1.0f, // Bottom-right
            x + w, y, z, 1.0f, 0.0f, // Top-right
            x, y, z, 0.0f, 0.0f // Top-left
        ];

        Span<uint> indices = [0, 1, 3, 1, 2, 3];

        _quadVBO = new BufferObject<float>(vertices, BufferTarget.ArrayBuffer);
        _quadEBO = new BufferObject<uint>(
            indices,
            BufferTarget.ElementArrayBuffer
        );
    }

    /// <summary>
    ///     Renders all quads stored by using the given camera's ProjectionMatrix.
    /// </summary>
    /// <param name="camera">The required camera.</param>
    public void Render(Camera camera)
    {
        _shader.Use();
        _shader.SetUniform("uViewProj", camera.GetVPMatrix());

        _arrayVAO.Bind();
        _arrayVAO.Update();

        GL.DrawElementsInstanced(
            PrimitiveType.Triangles,
            _quadEBO.GetCount(),
            DrawElementsType.UnsignedInt,
            IntPtr.Zero,
            _array.Length
        );
    }

    public void SetDirty(int index)
    {
        _arrayVBO[index] = _array[index];
    }
}
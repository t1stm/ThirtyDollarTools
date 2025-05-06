using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Renderer.Instanced;

/// <summary>
/// Represents a collection of quads that can only be allocated once.
/// When the size is changed due to user requirements the allocation must happen with a new object.
/// </summary>
public class QuadArray : IDisposable
{
    private BufferObject<float>? _quadVBO;
    private BufferObject<Quad> _arrayVBO = null!;
    private VertexArrayObject _arrayVAO = null!;
    private readonly Quad[] _array;

    public QuadArray(Quad[] array)
    {
        _array = array;
        UploadToGPU();
    }

    protected void UploadToGPU()
    {
        if (_quadVBO == null)
            InitQuadVBO();

        _arrayVBO = new BufferObject<Quad>(_array, BufferTarget.ArrayBuffer);
        _arrayVAO = new VertexArrayObject();

        var layout = new VertexBufferLayout();
        layout
            .PushFloat(3)   // position x,y,z
            .PushFloat(2);  // texture UV 
        
        _arrayVAO.AddBuffer(_quadVBO!, layout);
        _arrayVAO.AddBuffer(_arrayVBO, layout);
    }

    protected void InitQuadVBO()
    {
        var (x, y, z) = (0f, 0f, 0);
        var (w, h) = (1f, 1f);
        
        Span<float> verteces = [
            // Positions                    // Texture Coordinates
            x, y + h, z, 0.0f, 1.0f,        // Bottom-left
            x + w, y + h, z, 1.0f, 1.0f,    // Bottom-right
            x + w, y, z, 1.0f, 0.0f,        // Top-right
            x, y, z, 0.0f, 0.0f             // Top-left
        ];
        
        _quadVBO = new BufferObject<float>(verteces, BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw);
    }
    
    /// <summary>
    /// Renders all quads stored by using the given camera's ProjectionMatrix.
    /// </summary>
    /// <param name="camera">The required camera.</param>
    public void Render(Camera camera)
    {
        
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
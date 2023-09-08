using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects;

public abstract class Renderable
{
    public List<Renderable> Children = new();
    protected Vector3 Position { get; set; }
    protected Vector3 Scale { get; set; }
    protected Vector3 Offset { get; set; }
    protected Vector4 Color { get; set; }
    
    protected Shader Shader = null!;
    
    protected BufferObject<uint>? Ebo;
    protected BufferObject<float>? Vbo;
    protected VertexArrayObject<float>? Vao;

    protected readonly object LockObject = new();

    /// <summary>
    /// A boolean made for external use.
    /// </summary>
    public bool IsBeingUpdated = false;

    public virtual void Render(Camera camera)
    {
        foreach (var child in Children)
        {
            child.Render(camera);
        }
    }
    
    public virtual void Dispose() {}
    public void ChangeShader(Shader shader) => Shader = shader;
    public abstract void UpdateVertices();

    public Vector3 GetScale()
    {
        lock (LockObject)
            return Scale;
    }
    public Vector3 GetPosition()
    {
        lock (LockObject)
            return Position;
    }
    public Vector4 GetColor()
    {
        lock (LockObject)
            return Color;
    }
    
    public Vector3 GetOffset()
    {
        lock (LockObject)
            return Offset;
    }
    
    public void SetOffset(Vector3 position)
    {
        lock (LockObject)
            Offset = position;

        foreach (var child in Children)
        {
            child.SetOffset(position);
        }
    }
    public void SetPosition(Vector3 position)
    {
        lock (LockObject)
            Position = position;
        
        UpdateVertices();
    }

    public void SetScale(Vector3 scale)
    {
        lock (LockObject)
            Scale = scale;
        
        UpdateVertices();
    }

    public void SetColor(Vector4 color)
    {
        lock (LockObject)
            Color = color;
    }
}
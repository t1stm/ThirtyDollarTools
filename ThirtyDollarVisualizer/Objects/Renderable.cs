using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects;

public abstract class Renderable
{
    public readonly List<Renderable> Children = new();
    
    /// <summary>
    /// The model matrix of the current renderable.
    /// </summary>
    protected Matrix4 Model { get; set; }

    /// <summary>
    /// The position of the current renderable.
    /// </summary>
    protected Vector3 _position;
    
    /// <summary>
    /// The offset position of the current renderable. Intended for dynamic positioning (eg. animations)
    /// </summary>
    protected Vector3 _translation;
    
    /// <summary>
    /// The scale of the current renderable.
    /// </summary>
    protected Vector3 _scale;
    
    /// <summary>
    /// The rotation of the current renderable.
    /// </summary>
    protected Vector3 _rotation;
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

    protected virtual void UpdateModel()
    {
        var model = Matrix4.Identity;

        var position = GetPosition();
        var scale = GetScale();
        var translation = GetTranslation();

        model *= Matrix4.CreateScale(scale);
        model *= Matrix4.CreateTranslation(position + translation);

        SetModel(model);
    }

    public virtual void Render(Camera camera)
    {
        foreach (var child in Children)
        {
            child.Render(camera);
        }
    }

    public virtual void Dispose()
    {
        Ebo?.Dispose();
        Vbo?.Dispose();
        Vao?.Dispose();
    }
    
    public void ChangeShader(Shader shader) => Shader = shader;
    public abstract void SetVertices();
    
    public Vector4 GetColor()
    {
        lock (LockObject)
            return Color;
    }

    public Vector3 GetPosition()
    {
        lock (LockObject)
            return _position;
    }
    
    public Vector3 GetTranslation()
    {
        lock (LockObject)
            return _translation;
    }

    public Vector3 GetScale()
    {
        lock (LockObject)
            return _scale;
    }

    public Vector3 GetRotation()
    {
        lock (LockObject)
            return _rotation;
    }
    
    public void SetModel(Matrix4 model)
    {
        lock (LockObject)
            Model = model;

        foreach (var child in Children)
        {
            child.SetTranslation(GetTranslation());
            child.SetRotation(GetRotation());
        }
    }

    public void SetColor(Vector4 color)
    {
        lock (LockObject)
            Color = color;
    }

    public void SetPosition(Vector3 position)
    {
        lock (LockObject)
            _position = position;
        
        UpdateModel();
    }
    
    public void SetTranslation(Vector3 translation)
    {
        lock (LockObject)
            _translation = translation;
        
        UpdateModel();
    }
    
    public void SetScale(Vector3 scale)
    {
        lock (LockObject)
            _scale = scale;
        
        UpdateModel();
    }
    
    public void SetRotation(Vector3 value)
    {
        lock (LockObject)
            _rotation = value;
        
        UpdateModel();
    }
}
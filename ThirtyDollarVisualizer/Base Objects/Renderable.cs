using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Animations;
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

    private bool IsChild;

    public virtual void UpdateModel(bool is_child, params Animation[] animations)
    {
        IsChild = is_child;
        var model = Matrix4.Identity;

        var position = GetPosition();
        var scale = GetScale();
        var translation = GetTranslation();

        var temp_translation = Vector3.Zero;
        var temp_scale = Vector3.One;
        var temp_rotation = Vector3.Zero;
        
        foreach (var animation in animations)
        {
            if (IsChild && animation.AffectsChildren || !IsChild)
            {
                ComputeAnimation(animation, ref temp_translation, ref temp_scale, ref temp_rotation);
            }
        }
        
        var final_translation = position + translation + temp_translation;
        var final_scale = scale * temp_scale;
        var final_rotation = _rotation + temp_rotation;
        
        model *= Matrix4.CreateScale(final_scale);
        
        model *= Matrix4.CreateRotationX(final_rotation.X) * 
                 Matrix4.CreateRotationY(final_rotation.Y) * 
                 Matrix4.CreateRotationZ(final_rotation.Z);

        model *= Matrix4.CreateTranslation(final_translation);
        
        foreach (var renderable in Children)
        {
            renderable.UpdateModel(true, animations);
        }
        
        SetModel(model);
    }

    private void ComputeAnimation(Animation animation, ref Vector3 final_translation, ref Vector3 final_scale,
        ref Vector3 final_rotation)
    {
        var bit_stack = animation.Features;

        if (bit_stack.IsEnabled(AnimationFeature.Transform_Multiply))
        {
            var transform_multiply = animation.GetTransform_Multiply(this);
            if (transform_multiply != Vector3.One)
                final_translation *= transform_multiply;
        }

        if (bit_stack.IsEnabled(AnimationFeature.Transform_Add))
        {
            var transform_add = animation.GetTransform_Add(this);
            if (transform_add != Vector3.One)
                final_translation += transform_add;
        }

        if (bit_stack.IsEnabled(AnimationFeature.Scale_Multiply))
        {
            var s = animation.GetScale_Multiply(this);

            if (s != Vector3.One)
                final_scale *= s;
        }

        if (bit_stack.IsEnabled(AnimationFeature.Scale_Add))
        {
            var s = animation.GetScale_Add(this);

            if (s != Vector3.One)
                final_scale += s;
        }

        if (bit_stack.IsEnabled(AnimationFeature.Rotation_Add))
        {
            var rotation = animation.GetRotation_XYZ(this);

            if (rotation != Vector3.Zero)
                final_rotation += rotation;
        }

        if (!bit_stack.IsEnabled(AnimationFeature.Color_Value)) return;

        var color_change = animation.GetColor_Value(this);
        if (color_change != Vector4.Zero)
            Color = color_change;
    }

    public virtual void Render(Camera camera)
    {
        foreach (var child in Children)
        {
            child.Render(camera);
        }
    }

    public abstract void SetShaderUniforms(Camera camera);

    public virtual void Dispose()
    {
        Ebo?.Dispose();
        Vbo?.Dispose();
        Vao?.Dispose();
    }
    
    public void ChangeShader(Shader shader) => Shader = shader;
    
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
        
        UpdateModel(IsChild);
    }
    
    public void SetTranslation(Vector3 translation)
    {
        lock (LockObject)
            _translation = translation;
        
        UpdateModel(IsChild);

        foreach (var renderable in Children)
        {
            renderable.SetTranslation(translation);
        }
    }
    
    public void SetScale(Vector3 scale)
    {
        lock (LockObject)
            _scale = scale;
        
        UpdateModel(IsChild);
    }
    
    public void SetRotation(Vector3 value)
    {
        lock (LockObject)
            _rotation = value;
        
        UpdateModel(IsChild);
    }
}
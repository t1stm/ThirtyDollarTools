using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects;

public abstract class Renderable
{
    public readonly List<Renderable> Children = new();

    protected readonly object LockObject = new();

    /// <summary>
    ///     The position of the current renderable.
    /// </summary>
    protected Vector3 _position;

    /// <summary>
    ///     The rotation of the current renderable.
    /// </summary>
    protected Vector3 _rotation;

    /// <summary>
    ///     The scale of the current renderable.
    /// </summary>
    protected Vector3 _scale;

    /// <summary>
    ///     The offset position of the current renderable. Intended for dynamic positioning (eg. animations)
    /// </summary>
    protected Vector3 _translation;

    protected BufferObject<uint>? Ebo;

    /// <summary>
    ///     A boolean made for external use.
    /// </summary>
    public bool IsBeingUpdated = false;

    public bool IsChild;

    /// <summary>
    ///     Sets whether this renderable calls it's render method.
    /// </summary>
    public bool IsVisible = true;

    protected Shader Shader = null!;
    protected VertexArrayObject<float>? Vao;
    protected BufferObject<float>? Vbo;

    /// <summary>
    ///     The model matrix of the current renderable.
    /// </summary>
    protected Matrix4 Model { get; set; }

    protected Vector4 Color { get; set; }

    /// <summary>
    ///     Updates the current renderable's model for the MVP rendering method.
    /// </summary>
    /// <param name="is_child">Whether the current renderable is a child of an other renderable.</param>
    /// <param name="animations">The animations the current renderable will use.</param>
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
            if ((IsChild && animation.AffectsChildren) || !IsChild)
                ComputeAnimation(animation, ref temp_translation, ref temp_scale, ref temp_rotation);

        var final_translation = position + translation + temp_translation;
        var final_scale = scale * temp_scale;
        var final_rotation = _rotation + temp_rotation;

        model *= Matrix4.CreateScale(final_scale);

        model *= Matrix4.CreateRotationX(final_rotation.X) *
                 Matrix4.CreateRotationY(final_rotation.Y) *
                 Matrix4.CreateRotationZ(final_rotation.Z);

        model *= Matrix4.CreateTranslation(final_translation);

        foreach (var renderable in Children) renderable.UpdateModel(true, animations);

        SetModel(model);
    }

    /// <summary>
    ///     Computes a given animation.
    /// </summary>
    /// <param name="animation">The given animation.</param>
    /// <param name="final_translation">Reference to the final translation.</param>
    /// <param name="final_scale">Reference to the final scale.</param>
    /// <param name="final_rotation">Reference to the final rotation.</param>
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

    /// <summary>
    ///     Renders a given renderable using the projection matrix from the camera.
    /// </summary>
    /// <param name="camera">The camera you want to use.</param>
    public virtual void Render(Camera camera)
    {
        foreach (var child in Children) child.Render(camera);
    }

    /// <summary>
    ///     Method that sets the shader's uniforms if overriden.
    /// </summary>
    /// <param name="camera">The camera that contains main projection matrix.</param>
    public virtual void SetShaderUniforms(Camera camera)
    {
        // Implement if needed.
    }

    /// <summary>
    ///     A method that disposes the objects.
    /// </summary>
    public virtual void Dispose()
    {
        Ebo?.Dispose();
        Vbo?.Dispose();
        Vao?.Dispose();
    }

    /// <summary>
    ///     Changes the Renderable's shader to the given one.
    /// </summary>
    /// <param name="shader">The given shader.</param>
    public void ChangeShader(Shader shader)
    {
        Shader = shader;
    }

    /// <summary>
    ///     Gets the renderable's color.
    /// </summary>
    /// <returns></returns>
    public virtual Vector4 GetColor()
    {
        lock (LockObject)
        {
            return Color;
        }
    }

    /// <summary>
    ///     Gets the renderable's position.
    /// </summary>
    /// <returns>A Vector3 representing the position.</returns>
    public virtual Vector3 GetPosition()
    {
        lock (LockObject)
        {
            return _position;
        }
    }

    /// <summary>
    ///     Gets the renderable's translation.
    /// </summary>
    /// <returns>A Vector3 representing the translation.</returns>
    public virtual Vector3 GetTranslation()
    {
        lock (LockObject)
        {
            return _translation;
        }
    }

    /// <summary>
    ///     Gets the renderable's scale.
    /// </summary>
    /// <returns>A Vector3 representing the scale.</returns>
    public virtual Vector3 GetScale()
    {
        lock (LockObject)
        {
            return _scale;
        }
    }

    /// <summary>
    ///     Gets the renderable's rotation.
    /// </summary>
    /// <returns>A Vector3 representing the rotation.</returns>
    public virtual Vector3 GetRotation()
    {
        lock (LockObject)
        {
            return _rotation;
        }
    }

    /// <summary>
    ///     Sets the renderable's model.
    ///     <param name="model">The model.</param>
    /// </summary>
    public virtual void SetModel(Matrix4 model)
    {
        lock (LockObject)
        {
            Model = model;
        }
    }

    /// <summary>
    ///     Sets the renderable's model.
    ///     <param name="color">The color.</param>
    /// </summary>
    public virtual void SetColor(Vector4 color)
    {
        lock (LockObject)
        {
            Color = color;
        }
    }

    /// <summary>
    ///     Sets the renderable's position.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <param name="align">The align type.</param>
    /// <exception cref="ArgumentOutOfRangeException">Invalid PositionAlign given.</exception>
    public virtual void SetPosition(Vector3 position, PositionAlign align = PositionAlign.TopLeft)
    {
        lock (LockObject)
        {
            _position = align switch
            {
                PositionAlign.TopLeft => position,
                PositionAlign.TopCenter => position - _scale.X / 2f * Vector3.UnitX,
                PositionAlign.TopRight => position - _scale.X * Vector3.UnitX,

                PositionAlign.MiddleLeft => position - _scale.Y / 2f * Vector3.UnitY,
                PositionAlign.Center => position - _scale.Y / 2f * Vector3.UnitY - _scale.X / 2f * Vector3.UnitX,
                PositionAlign.MiddleRight => position - _scale.Y / 2f * Vector3.UnitY - _scale.X * Vector3.UnitX,

                PositionAlign.BottomLeft => position - _scale.Y * Vector3.UnitY,
                PositionAlign.BottomCenter => position - _scale.Y * Vector3.UnitY - _scale.X / 2f * Vector3.UnitX,
                PositionAlign.BottomRight => position - _scale.Y * Vector3.UnitY - _scale.X * Vector3.UnitX,
                _ => throw new ArgumentOutOfRangeException(nameof(align), align,
                    "Invalid Position Align in set position method.")
            };
        }

        UpdateModel(IsChild);
    }

    /// <summary>
    ///     Sets the renderable's translation.
    /// </summary>
    /// <param name="translation">The translation.</param>
    public virtual void SetTranslation(Vector3 translation)
    {
        lock (LockObject)
        {
            _translation = translation;
        }

        UpdateModel(IsChild);

        foreach (var renderable in Children) renderable.SetTranslation(translation);
    }

    /// <summary>
    ///     Sets the renderable's scale.
    /// </summary>
    /// <param name="scale">The scale.</param>
    public virtual void SetScale(Vector3 scale)
    {
        lock (LockObject)
        {
            _scale = scale;
        }

        UpdateModel(IsChild);
    }

    /// <summary>
    ///     Sets the renderable's rotation.
    /// </summary>
    /// <param name="value">The rotation.</param>
    public virtual void SetRotation(Vector3 value)
    {
        lock (LockObject)
        {
            _rotation = value;
        }

        UpdateModel(IsChild);
    }

    ~Renderable()
    {
        Children.Clear();
    }
}

public static class RenderableExtensions
{
    /// <summary>
    ///     Gives a Renderable object with its position set to the value you give.
    /// </summary>
    /// <param name="renderable">The source renderable.</param>
    /// <param name="position">The new position.</param>
    /// <param name="align">The position's align.</param>
    /// <returns>The source renderable with the new position set.</returns>
    public static Renderable WithPosition(this Renderable renderable, Vector3 position,
        PositionAlign align = PositionAlign.TopLeft)
    {
        renderable.SetPosition(position, align);
        return renderable;
    }

    /// <summary>
    ///     Gives a Renderable object with its translation set to the value you give.
    /// </summary>
    /// <param name="renderable">The source renderable.</param>
    /// <param name="translation">The new translation.</param>
    /// <returns>The source renderable with the new translation set.</returns>
    public static Renderable WithTranslation(this Renderable renderable, Vector3 translation)
    {
        renderable.SetTranslation(translation);
        return renderable;
    }

    /// <summary>
    ///     Gives a Renderable object with its rotation set to the value you give.
    /// </summary>
    /// <param name="renderable">The source renderable.</param>
    /// <param name="rotation">The new rotation.</param>
    /// <returns>The source renderable with the new rotation set.</returns>
    public static Renderable WithRotation(this Renderable renderable, Vector3 rotation)
    {
        renderable.SetRotation(rotation);
        return renderable;
    }

    /// <summary>
    ///     Gives a Renderable object with its scale set to the value you give.
    /// </summary>
    /// <param name="renderable">The source renderable.</param>
    /// <param name="scale">The new scale.</param>
    /// <returns>The source renderable with the new scale set.</returns>
    public static Renderable WithScale(this Renderable renderable, Vector3 scale)
    {
        renderable.SetScale(scale);
        return renderable;
    }

    /// <summary>
    ///     Gives a Renderable object with its color set to the value you give.
    /// </summary>
    /// <param name="renderable">The source renderable.</param>
    /// <param name="color">The new color.</param>
    /// <returns>The source renderable with the new color set.</returns>
    public static Renderable WithColor(this Renderable renderable, Vector4 color)
    {
        renderable.SetColor(color);
        return renderable;
    }
}

/// <summary>
///     Enum that sets how a position is interpreted.
/// </summary>
public enum PositionAlign : byte
{
    // These values follow bitwise rules.
    // Let's say that we have a byte 0000_0000
    // We only use the first six bits for the location.
    // Reading the value from right to left, the first three bits are for the X-axis,
    // and the second three for the Y-axis.

    // Example: TopRight: 001_001, TopCenter 001_010, Center 010_010, BottomLeft 100_100
    TopLeft = 8 ^ 4,
    TopCenter = 8 ^ 2,
    TopRight = 8 ^ 1,
    MiddleLeft = 16 ^ 4,
    Center = 16 ^ 2,
    MiddleRight = 16 ^ 1,
    BottomLeft = 32 ^ 4,
    BottomCenter = 32 ^ 2,
    BottomRight = 32 ^ 1
}
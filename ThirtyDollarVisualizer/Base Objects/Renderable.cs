using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Base_Objects.Text;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Base_Objects;

public abstract class Renderable
{
    /// <summary>
    /// Dummy renderable to use on animations that don't animate a renderable.
    /// </summary>
    public static readonly Renderable Dummy = new StaticText();

    public readonly List<Renderable> Children = [];

    /// <summary>
    /// A boolean made for external use.
    /// </summary>
    public bool IsBeingUpdated = false;

    public bool IsChild;

    /// <summary>
    /// Sets whether this renderable calls it's render method.
    /// </summary>
    public bool IsVisible = true;

    public virtual Shader? Shader { get; set; }

    /// <summary>
    /// The position of the current renderable.
    /// </summary>
    public virtual Vector3 Position { get; set; }

    /// <summary>
    /// The rotation of the current renderable.
    /// </summary>
    public virtual Vector3 Rotation { get; set; }

    /// <summary>
    /// The scale of the current renderable.
    /// </summary>
    public virtual Vector3 Scale { get; set; }

    /// <summary>
    /// The offset position of the current renderable. Intended for dynamic positioning (eg. animations)
    /// </summary>
    public virtual Vector3 Translation { get; set; }

    /// <summary>
    /// The model matrix of the current renderable.
    /// </summary>
    public virtual Matrix4 Model { get; set; }

    /// <summary>
    /// The color of the current renderable.
    /// </summary>
    public virtual Vector4 Color { get; set; }

    /// <summary>
    /// Represents the inverse alpha value used in renderable objects to influence transparency or animation.
    /// </summary>
    protected float InverseAlpha { get; set; }

    /// <summary>
    /// Updates the current renderable's model for the MVP rendering method.
    /// </summary>
    /// <param name="isChild">Whether the current renderable is a child of an other renderable.</param>
    /// <param name="animations">The animations the current renderable will use.</param>
    public virtual void UpdateModel(bool isChild, Span<Animation> animations = default)
    {
        IsChild = isChild;
        var model = Matrix4.Identity;

        var position = Position;
        var scale = Scale;
        var translation = Translation;

        var temp_translation = Vector3.Zero;
        var temp_scale = Vector3.One;
        var temp_rotation = Vector3.Zero;

        foreach (var animation in animations)
            if ((IsChild && animation.AffectsChildren) || !IsChild)
                ComputeAnimation(animation, ref temp_translation, ref temp_scale, ref temp_rotation);

        var final_translation = position + translation + temp_translation;
        var final_scale = scale * temp_scale;
        var final_rotation = Rotation + temp_rotation;

        model *= Matrix4.CreateScale(final_scale);

        model *= Matrix4.CreateRotationX(final_rotation.X) *
                 Matrix4.CreateRotationY(final_rotation.Y) *
                 Matrix4.CreateRotationZ(final_rotation.Z);

        model *= Matrix4.CreateTranslation(final_translation);

        foreach (var renderable in Children) renderable.UpdateModel(true, animations);

        Model = model;
    }

    /// <summary>
    /// Computes a given animation.
    /// </summary>
    /// <param name="animation">The given animation.</param>
    /// <param name="finalTranslation">Reference to the final translation.</param>
    /// <param name="finalScale">Reference to the final scale.</param>
    /// <param name="finalRotation">Reference to the final rotation.</param>
    private void ComputeAnimation(Animation animation, ref Vector3 finalTranslation, ref Vector3 finalScale,
        ref Vector3 finalRotation)
    {
        var bit_stack = animation.Features;

        if (bit_stack.IsEnabled(AnimationFeature.TransformMultiply))
        {
            var transform_multiply = animation.GetTransform_Multiply(this);
            if (transform_multiply != Vector3.One)
                finalTranslation *= transform_multiply;
        }

        if (bit_stack.IsEnabled(AnimationFeature.TransformAdd))
        {
            var transform_add = animation.GetTransform_Add(this);
            if (transform_add != Vector3.One)
                finalTranslation += transform_add;
        }

        if (bit_stack.IsEnabled(AnimationFeature.ScaleMultiply))
        {
            var s = animation.GetScale_Multiply(this);

            if (s != Vector3.One)
                finalScale *= s;
        }

        if (bit_stack.IsEnabled(AnimationFeature.ScaleAdd))
        {
            var s = animation.GetScale_Add(this);

            if (s != Vector3.One)
                finalScale += s;
        }

        if (bit_stack.IsEnabled(AnimationFeature.RotationAdd))
        {
            var rotation = animation.GetRotation_XYZ(this);

            if (rotation != Vector3.Zero)
                finalRotation += rotation;
        }

        if (bit_stack.IsEnabled(AnimationFeature.ColorValue))
        {
            var color_change = animation.GetColor_Value(this);
            if (color_change != Vector4.Zero)
                Color = color_change;
        }

        if (!bit_stack.IsEnabled(AnimationFeature.DeltaAlpha)) return;
        InverseAlpha = animation.GetAlphaDelta_Value(this);
    }

    /// <summary>
    /// Renders a given renderable using the projection matrix from the camera.
    /// </summary>
    /// <param name="camera">The camera you want to use.</param>
    public virtual void Render(Camera camera)
    {
        foreach (var child in Children) child.Render(camera);
    }

    /// <summary>
    /// Method that sets the shader's uniforms if overriden.
    /// </summary>
    /// <param name="camera">The camera that contains main projection matrix.</param>
    public virtual void SetShaderUniforms(Camera camera)
    {
        // Implement if needed.
    }

    /// <summary>
    /// Sets the renderable's position.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <param name="align">The align type.</param>
    /// <exception cref="ArgumentOutOfRangeException">Invalid PositionAlign given.</exception>
    public virtual void SetPosition(Vector3 position, PositionAlign align = PositionAlign.TopLeft)
    {
        var scale = Scale;

        Position = align switch
        {
            PositionAlign.TopLeft => position,
            PositionAlign.TopCenter => position - scale.X / 2f * Vector3.UnitX,
            PositionAlign.TopRight => position - scale.X * Vector3.UnitX,

            PositionAlign.MiddleLeft => position - scale.Y / 2f * Vector3.UnitY,
            PositionAlign.Center => position - scale.Y / 2f * Vector3.UnitY - scale.X / 2f * Vector3.UnitX,
            PositionAlign.MiddleRight => position - scale.Y / 2f * Vector3.UnitY - scale.X * Vector3.UnitX,

            PositionAlign.BottomLeft => position - scale.Y * Vector3.UnitY,
            PositionAlign.BottomCenter => position - scale.Y * Vector3.UnitY - scale.X / 2f * Vector3.UnitX,
            PositionAlign.BottomRight => position - scale.Y * Vector3.UnitY - scale.X * Vector3.UnitX,
            _ => throw new ArgumentOutOfRangeException(nameof(align), align,
                "Invalid Position Align in set position method.")
        };

        UpdateModel(IsChild);
    }

    /// <summary>
    /// Sets the renderable's translation.
    /// </summary>
    /// <param name="translation">The translation.</param>
    public virtual void SetTranslation(Vector3 translation)
    {
        Translation = translation;
        UpdateModel(IsChild);
        foreach (var renderable in Children) renderable.SetTranslation(translation);
    }

    ~Renderable()
    {
        Children.Clear();
    }
}

public static class RenderableExtensions
{
    public static TTarget? As<TTarget>(this Renderable renderable)
        where TTarget : Renderable
    {
        return renderable as TTarget;
    }

    /// <summary>
    /// Gives a Renderable object with its position set to the value you give.
    /// </summary>
    /// <param name="renderable">The source renderable.</param>
    /// <param name="position">The new position.</param>
    /// <param name="align">The position's align.</param>
    /// <returns>The source renderable with the new position set.</returns>
    public static T WithPosition<T>(this T renderable, Vector3 position,
        PositionAlign align = PositionAlign.TopLeft) where T : Renderable
    {
        renderable.SetPosition(position, align);
        return renderable;
    }

    /// <summary>
    /// Gives a Renderable object with its translation set to the value you give.
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
    /// Gives a Renderable object with its rotation set to the value you give.
    /// </summary>
    /// <param name="renderable">The source renderable.</param>
    /// <param name="rotation">The new rotation.</param>
    /// <returns>The source renderable with the new rotation set.</returns>
    public static Renderable WithRotation(this Renderable renderable, Vector3 rotation)
    {
        renderable.Rotation = rotation;
        return renderable;
    }

    /// <summary>
    /// Gives a Renderable object with its scale set to the value you give.
    /// </summary>
    /// <param name="renderable">The source renderable.</param>
    /// <param name="scale">The new scale.</param>
    /// <returns>The source renderable with the new scale set.</returns>
    public static Renderable WithScale(this Renderable renderable, Vector3 scale)
    {
        renderable.Scale = scale;
        return renderable;
    }

    /// <summary>
    /// Gives a Renderable object with its color set to the value you give.
    /// </summary>
    /// <param name="renderable">The source renderable.</param>
    /// <param name="color">The new color.</param>
    /// <returns>The source renderable with the new color set.</returns>
    public static Renderable WithColor(this Renderable renderable, Vector4 color)
    {
        renderable.Color = color;
        return renderable;
    }
}

/// <summary>
/// Enum that sets how a position is interpreted.
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
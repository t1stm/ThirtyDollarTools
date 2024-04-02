using System.Diagnostics;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Animations;

public abstract class Animation
{
    protected readonly Stopwatch TimingStopwatch;
    public bool AffectsChildren = true;
    protected TimeSpan AnimationLength;
    protected Action? CallbackOnFinish = null;
    public int Features = (int)AnimationFeature.None;

    protected Animation(int animation_length_ms) : this(TimeSpan.FromMilliseconds(animation_length_ms))
    {
    }

    protected Animation(TimeSpan timespan)
    {
        TimingStopwatch = new Stopwatch();
        AnimationLength = timespan;
    }

    public bool IsRunning => TimingStopwatch.IsRunning;

    /// <summary>
    ///     Gets the current position multiplication transform of this animation.
    /// </summary>
    /// <param name="renderable">The renderable the animation is used on.</param>
    /// <returns>A vector containing the transformations.</returns>
    public virtual Vector3 GetTransform_Multiply(Renderable renderable)
    {
        return Vector3.One;
    }

    /// <summary>
    ///     Gets the current position add transform of this animation.
    /// </summary>
    /// <param name="renderable">The renderable the animation is used on.</param>
    /// <returns>A vector containing the transformations.</returns>
    public virtual Vector3 GetTransform_Add(Renderable renderable)
    {
        return Vector3.Zero;
    }

    /// <summary>
    ///     Gets the current scale multiplication transform of this animation.
    /// </summary>
    /// <param name="renderable">The renderable the animation is used on.</param>
    /// <returns>A vector containing the scale.</returns>
    public virtual Vector3 GetScale_Multiply(Renderable renderable)
    {
        return Vector3.One;
    }

    /// <summary>
    ///     Gets the current scale add transform of this animation.
    /// </summary>
    /// <param name="renderable">The renderable the animation is used on.</param>
    /// <returns>A vector containing the scale.</returns>
    public virtual Vector3 GetScale_Add(Renderable renderable)
    {
        return Vector3.Zero;
    }

    /// <summary>
    ///     Gets the current rotation add transform on all axises.
    /// </summary>
    /// <param name="renderable">The renderable the animation is used on.</param>
    /// <returns>A vector containing the rotation.</returns>
    public virtual Vector3 GetRotation_XYZ(Renderable renderable)
    {
        return Vector3.Zero;
    }

    /// <summary>
    ///     Gets the current color add transform.
    /// </summary>
    /// <param name="renderable">The renderable the animation is used on.</param>
    /// <returns>A vector containing the color difference.</returns>
    public virtual Vector4 GetColor_Value(Renderable renderable)
    {
        return Vector4.Zero;
    }

    /// <summary>
    ///     Executes a given animation.
    /// </summary>
    public virtual void StartAnimation()
    {
        TimingStopwatch.Restart();
    }
}
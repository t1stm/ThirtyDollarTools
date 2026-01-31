using System.Diagnostics;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Helpers.Timing;

namespace ThirtyDollarVisualizer.Animations;

public abstract class Animation(TimeSpan timespan)
{
    public AnimationFeature Features { get; set; } = AnimationFeature.None;
    public bool AffectsChildren { get; set; } = true;

    protected SeekableStopwatch TimingStopwatch { get; } = new();
    protected TimeSpan AnimationLength { get; set; } = timespan;
    protected Action? CallbackOnFinish { get; set; }
    protected bool IsReset;

    protected Animation(int animationLengthMs) : this(TimeSpan.FromMilliseconds(animationLengthMs))
    {
    }

    public bool IsRunning => TimingStopwatch.IsRunning || (IsReset && !(IsReset = false));

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
    ///     Gets the current alpha subtract transform.
    /// </summary>
    /// <param name="renderable">The renderable the animation is used on.</param>
    /// <returns>A float representing the transparency to add.</returns>
    public virtual float GetAlphaDelta_Value(Renderable renderable)
    {
        return 0;
    }

    /// <summary>
    ///     Executes a given animation.
    /// </summary>
    public virtual void Start()
    {
        TimingStopwatch.Restart();
    }

    public virtual void Reset()
    {
        TimingStopwatch.Reset();
        IsReset = true;
    }
}
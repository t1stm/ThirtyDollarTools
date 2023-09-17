using System.Diagnostics;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Animations;

public abstract class Animation
{
    protected Stopwatch TimingStopwatch;
    protected TimeSpan AnimationLength;
    protected Action? CallbackOnFinish = null;

    protected Animation(int animation_length_ms): this(TimeSpan.FromMilliseconds(animation_length_ms))
    {
    }

    protected Animation(TimeSpan timespan)
    {
        TimingStopwatch = new Stopwatch();
        AnimationLength = timespan;
    }

    /// <summary>
    /// Gets the current position transform of this animation.
    /// </summary>
    /// <returns>A vector containing the transformations.</returns>
    public virtual Vector3 GetTransform() => Vector3.Zero;
    
    /// <summary>
    /// Gets the current scale transform of this animation.
    /// </summary>
    /// <returns>A vector containing the scale.</returns>
    public virtual Vector3 GetScale() => Vector3.One;
    
    /// <summary>
    /// Gets the current rotation transform on all axises.
    /// </summary>
    /// <returns>A vector containing the rotation.</returns>
    public virtual Vector3 GetRotation_XYZ => Vector3.Zero;
    
    /// <summary>
    /// Executes a given animation override.
    /// </summary>
    public virtual void StartAnimation()
    {
        TimingStopwatch.Restart();
    }
}
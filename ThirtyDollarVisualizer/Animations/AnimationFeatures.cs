namespace ThirtyDollarVisualizer.Animations;

/// <summary>
///     All features an animation can have.
///     Used in the renderer with if checks.
/// </summary>
[Flags]
public enum AnimationFeature
{
    None = 0,
    TransformMultiply = 1,
    TransformAdd = 1 << 1,
    ScaleMultiply = 1 << 2,
    ScaleAdd = 1 << 3,
    RotationAdd = 1 << 4,
    ColorValue = 1 << 5,
    DeltaAlpha = 1 << 6
}

public static class FeatureExtensions
{
    /// <summary>
    ///     Checks if the current animation feature is in a bit stack.
    /// </summary>
    /// <param name="feature">The feature you want to check for.</param>
    /// <param name="bitStack">The bit stack.</param>
    /// <returns>Whether the feature can is found in the bit stack.</returns>
    public static bool In(this AnimationFeature feature, AnimationFeature bitStack)
    {
        return (bitStack & feature) != 0;
    }

    /// <summary>
    ///     Checks if the current integer has an animation feature enabled.
    /// </summary>
    /// <param name="bitStack">The integer you want to check.</param>
    /// <param name="feature">The animation feature you want to check.</param>
    /// <returns>Whether the bit stack contains the feature. </returns>
    public static bool IsEnabled(this AnimationFeature bitStack, AnimationFeature feature)
    {
        return (bitStack & feature) != 0;
    }
}

// Note: When adding new features, only increment the bit offset.
// Stuff WILL break if you don't implement it correctly.
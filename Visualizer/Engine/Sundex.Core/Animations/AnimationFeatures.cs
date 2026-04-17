namespace Sundex.Core.Animations;

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
    /// <param name="feature">The feature you want to check for.</param>
    extension(AnimationFeature feature)
    {
        /// <summary>
        ///     Checks if the current animation feature is in a bit stack.
        /// </summary>
        /// <param name="bitStack">The bit stack.</param>
        /// <returns>Whether the feature can be found in the bit stack.</returns>
        public bool In(AnimationFeature bitStack)
        {
            return (bitStack & feature) != 0;
        }

        /// <summary>
        ///     Checks if the current integer has an animation feature enabled.
        /// </summary>
        /// <param name="feature1">The animation feature you want to check.</param>
        /// <returns>Whether the bit stack contains the feature. </returns>
        public bool IsEnabled(AnimationFeature feature1)
        {
            return (feature & feature1) != 0;
        }
    }
}

// Note: When adding new features, only increment the bit offset.
// Stuff WILL break if you don't implement it correctly.
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
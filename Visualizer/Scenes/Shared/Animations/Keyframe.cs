using OpenTK.Mathematics;

namespace Shared.Animations;

public struct Keyframe
{
    public Vector3 Position; // absolute px
    public float LengthMs; // self explanatory

    public Vector4? Color;
    public Vector3? Scale; // multiplier
    public float? Rotation; // radians
    public float? Opacity; // 0-1f

    public Vector3? BezierP1; // Optional Bezier Control Point 1
    public Vector3? BezierP2; // Optional Bezier Control Point 2
    public AnimationLoopingMode LoopingMode;
    public SteppingFunction SteppingFunction;
}
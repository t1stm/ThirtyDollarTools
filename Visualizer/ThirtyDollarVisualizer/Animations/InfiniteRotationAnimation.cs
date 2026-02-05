using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects;

namespace ThirtyDollarVisualizer.Animations;

public class InfiniteRotationAnimation : Animation
{
    private readonly bool _rotateX;
    private readonly bool _rotateY;
    private readonly bool _rotateZ;

    public InfiniteRotationAnimation(int animationLengthMs, int rotateAxes) : base(animationLengthMs)
    {
        Features = AnimationFeature.RotationAdd;
        TimingStopwatch.Start();

        _rotateX = (rotateAxes & (int)RotateAxis.X) > 0;
        _rotateY = (rotateAxes & (int)RotateAxis.Y) > 0;
        _rotateZ = (rotateAxes & (int)RotateAxis.Z) > 0;
    }

    public override Vector3 GetRotation_XYZ(Renderable renderable)
    {
        var current_rotation =
            TimingStopwatch.ElapsedMilliseconds % AnimationLength.TotalMilliseconds;

        var rotation_norm = current_rotation / AnimationLength.TotalMilliseconds;
        var radians = rotation_norm * 360f / Math.Tau;

        var rotation = new Vector3
        {
            X = _rotateX ? (float)radians : 0,
            Y = _rotateY ? (float)radians : 0,
            Z = _rotateZ ? (float)radians : 0
        };

        return rotation;
    }
}

public enum RotateAxis
{
    X = 1,
    Y = 1 << 1,
    Z = 1 << 2
}
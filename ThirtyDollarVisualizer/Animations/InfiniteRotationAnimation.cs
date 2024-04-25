using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Animations;

public class InfiniteRotationAnimation : Animation
{
    private readonly bool RotateX;
    private readonly bool RotateY;
    private readonly bool RotateZ;

    public InfiniteRotationAnimation(int animation_length_ms, int rotate_axises) : base(animation_length_ms)
    {
        Features = AnimationFeature.Rotation_Add;
        TimingStopwatch.Start();

        RotateX = (rotate_axises & (int)RotateAxis.X) > 0;
        RotateY = (rotate_axises & (int)RotateAxis.Y) > 0;
        RotateZ = (rotate_axises & (int)RotateAxis.Z) > 0;
    }

    public override Vector3 GetRotation_XYZ(Renderable renderable)
    {
        var current_rotation =
            TimingStopwatch.ElapsedMilliseconds % AnimationLength.TotalMilliseconds;

        var rotation_norm = current_rotation / AnimationLength.TotalMilliseconds;
        var radians = rotation_norm * 360f / Math.Tau;

        var rotation = new Vector3
        {
            X = RotateX ? (float)radians : 0,
            Y = RotateY ? (float)radians : 0,
            Z = RotateZ ? (float)radians : 0
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
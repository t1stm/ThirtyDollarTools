using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects;

namespace ThirtyDollarVisualizer.Animations;

public class ExpandAnimation : Animation
{
    private const int AnimationLengthMs = 250;
    private const float ExpandSize = .10f;
    private const float ExpandHalf = ExpandSize / 2f;

    public ExpandAnimation() : base(AnimationLengthMs)
    {
        Features = AnimationFeature.TransformAdd |
                   AnimationFeature.ScaleMultiply;
    }

    public ExpandAnimation(Action finishCallback) : this()
    {
        CallbackOnFinish = finishCallback;
    }

    public override Vector3 GetTransform_Add(Renderable renderable)
    {
        if (!TimingStopwatch.IsRunning) return base.GetTransform_Add(renderable);
        var transformation = new Vector3();

        var current_time = TimingStopwatch.ElapsedMilliseconds;
        var normalized = (float)Math.Max(current_time / AnimationLength.TotalMilliseconds, 0);

        if (normalized > 1)
        {
            TimingStopwatch.Stop();
            CallbackOnFinish?.Invoke();
            normalized = 1;
        }

        var factor = MathF.Sin(MathF.PI * normalized);
        var scale = renderable.Scale;

        transformation.X -= factor * ExpandHalf * scale.X;
        transformation.Y -= factor * ExpandHalf * scale.Y;

        return transformation;
    }

    public override Vector3 GetScale_Multiply(Renderable renderable)
    {
        if (!TimingStopwatch.IsRunning) return base.GetScale_Multiply(renderable);

        var current_time = TimingStopwatch.ElapsedMilliseconds;
        var normalized = (float)Math.Max(current_time / AnimationLength.TotalMilliseconds, 0);

        if (normalized > 1)
        {
            TimingStopwatch.Stop();
            CallbackOnFinish?.Invoke();
            normalized = 1;
        }

        var factor = MathF.Sin(MathF.PI * normalized);
        var scale_factor = 1 + factor * ExpandSize;
        var transformation = new Vector3(scale_factor);

        return transformation;
    }
}
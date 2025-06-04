using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects;

namespace ThirtyDollarVisualizer.Animations;

public class BounceAnimation : Animation
{
    private const int AnimationLengthMs = 400;
    public float FinalY;

    public BounceAnimation() : base(AnimationLengthMs)
    {
        Features = AnimationFeature.TransformAdd;
    }

    public BounceAnimation(Action finishCallback) : this()
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

        float factor;

        // Animation: % max, ease out on start, ease in after %
        const float maxPercent = 0.4f;
        if (normalized < maxPercent)
        {
            var temp_val = normalized / maxPercent / 2f;
            factor = MathF.Sin(MathF.PI * temp_val);
        }
        else
        {
            var temp_val = 0.5f + (normalized - maxPercent) / (1f - maxPercent) * 0.5f;
            factor = 1f - MathF.Cos(MathF.PI * (1f - temp_val));
        }

        transformation.Y = -factor * FinalY;

        return transformation;
    }
}
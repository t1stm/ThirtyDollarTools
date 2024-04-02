using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Animations;

public class BounceAnimation : Animation
{
    private const int AnimationLengthMs = 400;
    private readonly float Final_Y;

    public BounceAnimation(float final_y) : base(AnimationLengthMs)
    {
        Features = (int)AnimationFeature.Transform_Add;
        Final_Y = final_y;
    }

    public BounceAnimation(float final_y, Action finish_callback) : this(final_y)
    {
        CallbackOnFinish = finish_callback;
    }

    public override Vector3 GetTransform_Add(Renderable renderable)
    {
        if (!TimingStopwatch.IsRunning) return Vector3.Zero;

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
        const float max_percent = 0.4f;
        if (normalized < max_percent)
        {
            var temp_val = normalized / max_percent / 2f;
            factor = (float)Math.Sin(Math.PI * temp_val);
        }
        else
        {
            var temp_val = 0.5f + (normalized - max_percent) / (1f - max_percent) * 0.5f;
            factor = 1f - (float)Math.Cos(Math.PI * (1f - temp_val));
        }

        transformation.Y = -factor * Final_Y;

        return transformation;
    }
}
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Animations;

public class BounceAnimation : Animation
{
    private const int AnimationLengthMs = 500;
    private readonly float Final_Y;
    public BounceAnimation(float final_y) : base(AnimationLengthMs)
    {
        Features = (int) AnimationFeature.Transform_Add;
        Final_Y = final_y;
    }

    public BounceAnimation(float final_y, Action finish_callback): this(final_y)
    {
        CallbackOnFinish = finish_callback;
    }

    public override Vector3 GetTransform_Add(Renderable renderable)
    {
        if (!TimingStopwatch.IsRunning) return Vector3.Zero;
        
        var transformation = new Vector3();
        
        var current_time = TimingStopwatch.ElapsedMilliseconds;
        var normalized = (float) Math.Max(current_time / AnimationLength.TotalMilliseconds, 0);

        if (normalized > 1)
        {
            TimingStopwatch.Stop();
            CallbackOnFinish?.Invoke();
            normalized = 1;
        }
        
        var factor = (float)Math.Sin(Math.PI * normalized);
        transformation.Y = -factor * Final_Y;
        
        return transformation;
    }
}
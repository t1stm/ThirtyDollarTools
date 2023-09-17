using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Animations;

public class BounceAnimation : Animation
{
    public BounceAnimation() : base(500)
    {
    }

    public BounceAnimation(Action finish_callback): this()
    {
        CallbackOnFinish = finish_callback;
    }

    public override Vector3 GetTransform()
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
        transformation.Y = -factor * 20;
        
        return transformation;
    }
}
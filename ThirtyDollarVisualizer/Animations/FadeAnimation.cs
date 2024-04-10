using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Animations;

public class FadeAnimation : Animation
{
    public FadeAnimation(Action action) : this()
    {
        CallbackOnFinish = action;
        AffectsChildren = false;
    }

    public FadeAnimation() : base(125)
    {
        Features = (int)AnimationFeature.DeltaAlpha;
    }

    public override float GetAlphaDelta_Value(Renderable renderable)
    {
        const float max_delta_alpha = 0.4f;

        var factor = TimingStopwatch.ElapsedMilliseconds / (float)AnimationLength.TotalMilliseconds;
        if (!TimingStopwatch.IsRunning) return factor > 1f ? max_delta_alpha : 0;

        if (factor <= 1f) return factor * max_delta_alpha;

        TimingStopwatch.Stop();
        CallbackOnFinish?.Invoke();

        return max_delta_alpha;
    }
}
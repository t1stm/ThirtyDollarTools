using ThirtyDollarVisualizer.Base_Objects;

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
        Features = AnimationFeature.DeltaAlpha;
    }

    public override float GetAlphaDelta_Value(Renderable renderable)
    {
        const float maxDeltaAlpha = 0.4f;

        var factor = TimingStopwatch.ElapsedMilliseconds / (float)AnimationLength.TotalMilliseconds;
        if (!TimingStopwatch.IsRunning) return factor > 1f ? maxDeltaAlpha : 0;

        if (factor <= 1f) return factor * maxDeltaAlpha;

        TimingStopwatch.Stop();
        CallbackOnFinish?.Invoke();

        return maxDeltaAlpha;
    }
}
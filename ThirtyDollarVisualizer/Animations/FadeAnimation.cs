using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Animations;

public class FadeAnimation : Animation
{
    private static readonly Vector4 FadedColor = new(0, 0, 0, 0.4f);
    
    public FadeAnimation(Action action) : this()
    {
        CallbackOnFinish = action;
        AffectsChildren = false;
    }

    public FadeAnimation() : base(125)
    {
        Features = (int)AnimationFeature.Color_Value;   
    }

    public override Vector4 GetColor_Value(Renderable renderable)
    {
        var vector = Vector4.Zero;
        
        var factor = TimingStopwatch.ElapsedMilliseconds / (float) AnimationLength.TotalMilliseconds;
        if (!TimingStopwatch.IsRunning)
        {
            return factor > 1f ? FadedColor : vector;
        }

        if (factor <= 1f) return Vector4.Lerp(vector, FadedColor, factor);
        
        TimingStopwatch.Stop();
        CallbackOnFinish?.Invoke();
        factor = 1f;

        return Vector4.Lerp(vector, FadedColor, factor);
    }
}
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Animations;

public class MidiKeyPressAnimation : Animation
{
    public long PressedLength = 1000;
    public int FadeTime = 500;
    public Vector4 PressedColor = Vector4.One;
    public Vector4 ReleasedColor = (0, 0, 0, 1f);

    public MidiKeyPressAnimation(int fade_time, Action? finish_callback = null) : base(fade_time)
    {
        Features = (int)AnimationFeature.Color_Value;
        CallbackOnFinish = finish_callback;
    }

    public override Vector4 GetColor_Value(Renderable renderable)
    {
        if (!TimingStopwatch.IsRunning) return ReleasedColor;

        var time = TimingStopwatch.ElapsedMilliseconds;
        if (time < PressedLength) return PressedColor;

        time -= PressedLength;
        if (time < FadeTime)
            return Vector4.Lerp(PressedColor, ReleasedColor, Math.Max(0f, (float) time / FadeTime));
        
        TimingStopwatch.Stop();
        CallbackOnFinish?.Invoke();
        return ReleasedColor;
    }
}
using OpenTK.Mathematics;
using Sundex.Core;
using Sundex.Core.Animations;

namespace Shared.Animations;

public class PulseAnimation : Animation
{
    private const float MaxAddScale = 0.05f;
    private float _frequency;
    private int _repeats;

    public PulseAnimation(int repeats, float frequency) : base((int)(repeats * frequency))
    {
        _repeats = repeats;
        _frequency = frequency;
        Features = AnimationFeature.ScaleAdd;
    }

    public override Vector3 GetScale_Add(Renderable renderable)
    {
        if (!IsRunning) return Vector3.Zero;

        var elapsed = TimingStopwatch.ElapsedMilliseconds;
        var totalDuration = AnimationLength.TotalMilliseconds;

        if (elapsed >= totalDuration)
        {
            TimingStopwatch.Stop();
            CallbackOnFinish?.Invoke();
            return Vector3.Zero;
        }

        if (_frequency <= 0) return Vector3.Zero;

        var factor = elapsed % _frequency / _frequency;
        var zoom = MathF.Sin(MathF.PI * factor) * MaxAddScale;
        return new Vector3(zoom, zoom, zoom);
    }

    public void ResetWith(int repeats, float frequency)
    {
        _repeats = repeats;
        _frequency = frequency;
        AnimationLength = TimeSpan.FromMilliseconds(_repeats * _frequency);
        TimingStopwatch.Restart();
    }
}
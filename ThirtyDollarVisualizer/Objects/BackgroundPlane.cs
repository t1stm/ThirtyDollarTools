using System.Diagnostics;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects.Planes;

namespace ThirtyDollarVisualizer.Objects;

public class BackgroundPlane : ColoredPlane
{
    private readonly Stopwatch _timingStopwatch = new();

    private Vector4 _finalColor;
    private float _lengthMilliseconds;
    private Vector4 _startColor;

    public BackgroundPlane(Vector4 startColor)
    {
        // i don't want to convert to a primary constructor for this, thank you.
        _startColor = startColor;
        _finalColor = startColor;
        _timingStopwatch.Start();
    }

    public void Update()
    {
        Color = GetCalculatedColor();
    }

    private Vector4 GetCalculatedColor()
    {
        var current_time = _timingStopwatch.ElapsedMilliseconds;
        var value = current_time / _lengthMilliseconds;
        if (value > 1f) _timingStopwatch.Stop();

        if (_lengthMilliseconds == 0) value = 1;
        var factor = Math.Clamp(value, 0f, 1f);

        return Vector4.Lerp(_startColor, _finalColor, factor);
    }

    public void TransitionToColor(Vector4 color, float seconds)
    {
        _startColor = GetCalculatedColor();
        _finalColor = color;

        _lengthMilliseconds = seconds * 1000f;
        _timingStopwatch.Restart();
    }
}
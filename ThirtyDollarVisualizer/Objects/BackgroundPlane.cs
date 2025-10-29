using System.Diagnostics;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Renderer.Attributes;

namespace ThirtyDollarVisualizer.Objects;

[PreloadGL]
public class BackgroundPlane : ColoredPlane
{
    [UsedImplicitly]
    public new static void Preload()
    {
        // hacky solution I know.
        ColoredPlane.Preload();
    }
    
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

    public override void Update()
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
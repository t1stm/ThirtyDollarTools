using System.Diagnostics;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Objects;

public class BackgroundPlane : ColoredPlane
{
    private readonly Stopwatch TimingStopwatch = new();

    private Vector4 FinalColor;
    private float LengthMilliseconds;
    private Vector4 StartColor;

    public BackgroundPlane(Vector4 start_color, Vector3 position, Vector3 scale, Shader? shader) : base(start_color,
        position, scale, shader)
    {
        // i don't want to convert to a primary constructor for this, thank you.
        StartColor = start_color;
        FinalColor = start_color;
        TimingStopwatch.Start();
    }

    public void Update()
    {
        Color = GetCalculatedColor();
    }

    private Vector4 GetCalculatedColor()
    {
        var current_time = TimingStopwatch.ElapsedMilliseconds;
        var value = current_time / LengthMilliseconds;
        if (value > 1f) TimingStopwatch.Stop();

        if (LengthMilliseconds == 0) value = 1;
        var factor = Math.Clamp(value, 0f, 1f);

        return Vector4.Lerp(StartColor, FinalColor, factor);
    }

    public void TransitionToColor(Vector4 color, float seconds)
    {
        StartColor = GetCalculatedColor();
        FinalColor = color;

        LengthMilliseconds = seconds * 1000f;
        TimingStopwatch.Restart();
    }
}
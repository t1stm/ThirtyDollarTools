using System.Diagnostics;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;

namespace ThirtyDollarVisualizer.Objects.Planes;

[PreloadGraphicsContext]
public class FlashOverlayPlane(Vector4 peakColor, float stageOneTimeMs = 0.125f, float stageTwoTimeMs = 0.25f)
    : ColoredPlane
{
    private readonly float _lengthToEndMilliseconds = stageOneTimeMs + stageTwoTimeMs;

    private readonly Stopwatch _timingStopwatch = new();

    [UsedImplicitly]
    public new static void Preload(AssetProvider assetProvider)
    {
        ColoredPlane.Preload(assetProvider);
    }

    public override void Update()
    {
        Color = GetCalculatedColor();
    }

    private Vector4 GetCalculatedColor()
    {
        var currentTime = _timingStopwatch.ElapsedMilliseconds;
        var value = currentTime / stageOneTimeMs;
        if (value > 1f)
            return GetFadingColor(currentTime);

        if (stageOneTimeMs == 0) value = 1;
        var factor = Math.Clamp(value, 0f, 1f);

        return Vector4.Lerp(Vector4.Zero, peakColor, factor);
    }

    private Vector4 GetFadingColor(float currentTime)
    {
        currentTime -= stageOneTimeMs;
        var factor = currentTime / _lengthToEndMilliseconds;
        if (factor <= 1) return Vector4.Lerp(peakColor, Vector4.Zero, factor);

        _timingStopwatch.Stop();
        return Vector4.Zero;
    }

    public void Flash()
    {
        _timingStopwatch.Restart();
    }
}
using System.Diagnostics;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Helpers.Color;

public static class ColorTools
{
    private static readonly Vector4 FadeColor = new(0, 0, 0, 0.6f);
    
    public static async void Fade(Renderable renderable)
    {
        await ChangeColor(renderable, Vector4.Zero, FadeColor, 0.125f);
    }

    public static async Task ChangeColor(Renderable renderable, Vector4 color, float duration_seconds)
    {
        var old_color = renderable.GetColor();
        await ChangeColor(renderable, old_color, color, duration_seconds);
    }

    public static async Task ChangeColor(Renderable renderable, Vector4 old_color, Vector4 color, float duration_seconds)
    {
        if (renderable.IsBeingUpdated)
        {
            renderable.IsBeingUpdated = false;
            await Task.Delay(33);
            await ChangeColor(renderable, color, duration_seconds);
            return;
        }
        
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        renderable.IsBeingUpdated = true;
        float elapsed;
        while ((elapsed = stopwatch.ElapsedMilliseconds / 1000f) < duration_seconds && renderable.IsBeingUpdated)
        {
            var delta = (float) Math.Clamp(elapsed / duration_seconds, 0.01, 1);
            
            renderable.SetColor(Vector4.Lerp(old_color, color, delta));
            await Task.Delay(16);
        }
        
        renderable.SetColor(color);

        renderable.IsBeingUpdated = false;
        stopwatch.Stop();
    }
}
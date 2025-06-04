using System.Diagnostics;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects;

namespace ThirtyDollarVisualizer.Helpers.Color;

public static class ColorTools
{
    public static async Task ChangeColor(Renderable renderable, Vector4 color, float durationSeconds)
    {
        var old_color = renderable.Color;
        await ChangeColor(renderable, old_color, color, durationSeconds);
    }

    public static async Task ChangeColor(Renderable renderable, Vector4 oldColor, Vector4 color,
        float durationSeconds)
    {
        // TODO: port this to use the Animation abstract class

        if (renderable.IsBeingUpdated)
        {
            renderable.IsBeingUpdated = false;
            await Task.Delay(33);
            await ChangeColor(renderable, color, durationSeconds);
            return;
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        renderable.IsBeingUpdated = true;
        float elapsed;
        while ((elapsed = stopwatch.ElapsedMilliseconds / 1000f) < durationSeconds && renderable.IsBeingUpdated)
        {
            var delta = Math.Clamp(elapsed / durationSeconds, 0.01f, 1f);

            renderable.Color = Vector4.Lerp(oldColor, color, delta);
            await Task.Delay(16);
        }

        renderable.Color = color;

        renderable.IsBeingUpdated = false;
        stopwatch.Stop();
    }
}
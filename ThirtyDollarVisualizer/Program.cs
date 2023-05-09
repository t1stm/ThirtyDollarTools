using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

namespace ThirtyDollarVisualizer;

public static class Program
{
    public static void Main()
    {
        var settings = new NativeWindowSettings
        {
            MinimumSize = new Vector2i(640, 360),
            AspectRatio = (16, 9)
        };

        using var window = new Window(GameWindowSettings.Default, settings);
        window.Run();
    }
}
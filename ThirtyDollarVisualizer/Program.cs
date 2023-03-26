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
            Size = new Vector2i(800, 600)
        };

        using var window = new Window(GameWindowSettings.Default, settings);
        window.Run();
    }
}
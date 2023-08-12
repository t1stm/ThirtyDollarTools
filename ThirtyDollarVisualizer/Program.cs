using Silk.NET.Windowing;

// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

namespace ThirtyDollarVisualizer;

public static class Program
{
    public static void Main()
    {
        var options = WindowOptions.Default;

        var window = Window.Create(options);

        var manager = new Manager(window);

        window.Run();
        window.Dispose();
    }
}
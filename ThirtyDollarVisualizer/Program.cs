using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

namespace ThirtyDollarVisualizer
{
    public class Program
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
}
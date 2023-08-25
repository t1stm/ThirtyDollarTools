// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

namespace ThirtyDollarVisualizer;

public static class Program
{
    public static void Main()
    {
        var manager = new Manager(1920,900, "Thirty Dollar Visualizer");
        manager.Run();
    }
}
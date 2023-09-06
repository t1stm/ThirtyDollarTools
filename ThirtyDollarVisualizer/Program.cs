// Warm thanks to The Cherno
// https://youtube.com/playlist?list=PLlrATfBNZ98foTJPJ_Ev03o2oq3-GGOS2

namespace ThirtyDollarVisualizer;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("No composition specified.");
            return;
        }
        var composition = args[0]; 
        
        var manager = new Manager(1920,840, "Thirty Dollar Visualizer", composition);
        manager.Run();
    }
}
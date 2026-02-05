namespace ThirtyDollarVisualizer.Helpers.Logging;

public static class DefaultLogger
{
    public static void Init()
    {
#if RELEASE
        Console.SetOut(new StreamWriter("Visualizer.log", true));
        Console.WriteLine($"(${DateTime.Now:HH:mm:ss.fff}) Visualizer started.");
#endif
    }

    public static void Log(string message)
    {
        var datetime = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"({datetime}) {message}");
    }

    public static void Log(string context, string message)
    {
        Log($"[{context}]: {message}");
    }
}
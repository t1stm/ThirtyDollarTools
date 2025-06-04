namespace ThirtyDollarVisualizer.Helpers.Logging;

public static class DefaultLogger
{
    public static void Log(string message)
    {
        var datetime = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"({datetime}) {message}");
    }

    public static void Log(string context, string message) => Log($"[{context}]: {message}");
}
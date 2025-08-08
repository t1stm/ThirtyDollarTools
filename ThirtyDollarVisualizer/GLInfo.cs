namespace ThirtyDollarVisualizer;

public static class GLInfo
{
    public static readonly HashSet<string> Extensions = [];
    public static string Vendor { get; set; } = string.Empty;
    public static string Renderer { get; set; } = string.Empty;
    public static string Version { get; set; } = string.Empty;

    public static int MaxTexture2DSize { get; set; }
    public static int MaxTexture2DLayers { get; set; }
    
    public static bool SupportsKHRDebug { get; set; }
}
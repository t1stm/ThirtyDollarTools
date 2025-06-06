namespace ThirtyDollarVisualizer;

public static class GLInfo
{
    public static readonly HashSet<string> Extensions = [];
    public static string Vendor = string.Empty;
    public static string Renderer = string.Empty;
    public static string Version = string.Empty;

    public static int MaxTexture2DSize;
    public static int MaxTexture2DLayers;
}
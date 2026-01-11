namespace ThirtyDollarVisualizer.Engine;

public class GLInfo
{
    public readonly HashSet<string> Extensions = [];
    public string Vendor { get; set; } = string.Empty;
    public string Renderer { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    public int MaxTexture2DSize { get; set; }
    public int MaxTexture2DLayers { get; set; }
    
    public bool SupportsKHRDebug { get; set; }
}
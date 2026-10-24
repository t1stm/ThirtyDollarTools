namespace Sundex.Engine;

public class GLInfo
{
    // A property rather than a field so the startup {@GLInfo} log line carries the list.
    public HashSet<string> Extensions { get; } = [];
    public string Vendor { get; set; } = string.Empty;
    public string Renderer { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    public int MaxTexture2DSize { get; set; }
    public int MaxTexture2DLayers { get; set; }

    public bool SupportsKHRDebug { get; set; }
    public bool SupportsDirectStateAccess { get; set; }
}
using ThirtyDollarVisualizer.Engine.Asset_Management.Abstract;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Types.Cache;

public class CachedInfo : ILoaderInfo
{
    public string CacheID { get; set; } = string.Empty;
}
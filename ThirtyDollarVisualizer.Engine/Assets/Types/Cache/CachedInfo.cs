using ThirtyDollarVisualizer.Engine.Assets.Abstract;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Cache;

public class CachedInfo : ILoaderInfo
{
    public string CacheID { get; set; } = string.Empty;
}
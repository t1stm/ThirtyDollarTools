using Sundex.Engine.Asset_Management.Abstract;

namespace Sundex.Engine.Asset_Management.Types.Cache;

public class CachedInfo : ILoaderInfo
{
    public string CacheID { get; set; } = string.Empty;
}
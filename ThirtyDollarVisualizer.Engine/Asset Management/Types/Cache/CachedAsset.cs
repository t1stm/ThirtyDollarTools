using ThirtyDollarVisualizer.Engine.Asset_Management.Abstract;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Asset;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Types.Cache;

public class CachedAsset : ILoadableAsset<CachedAsset, CachedInfo>
{
    public static IAssetLoader<CachedAsset, CachedInfo> AssetLoader { get; } = new CachedAssetLoader();
    
    public AssetStream AssetStream { get; set; } = new();
}
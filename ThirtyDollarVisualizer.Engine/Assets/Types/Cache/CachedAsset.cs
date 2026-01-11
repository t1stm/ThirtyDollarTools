using ThirtyDollarVisualizer.Engine.Assets.Abstract;
using ThirtyDollarVisualizer.Engine.Assets.Types.Asset;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Cache;

public class CachedAsset : ILoadableAsset<CachedAsset, CachedInfo>
{
    public static IAssetLoader<CachedAsset, CachedInfo> AssetLoader { get; } = new CachedAssetLoader();
    
    public AssetStream AssetStream { get; set; } = new();
}
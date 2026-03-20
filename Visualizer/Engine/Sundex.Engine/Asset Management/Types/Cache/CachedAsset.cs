using Sundex.Engine.Asset_Management.Abstract;
using Sundex.Engine.Asset_Management.Abstract.Loading;
using Sundex.Engine.Asset_Management.Types.Asset;

namespace Sundex.Engine.Asset_Management.Types.Cache;

public class CachedAsset : ILoadableAsset<CachedAsset, CachedInfo>
{
    public AssetStream AssetStream { get; set; } = new();
    public static IAssetLoader<CachedAsset, CachedInfo> AssetLoader { get; } = new CachedAssetLoader();
}
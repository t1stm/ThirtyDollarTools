using ThirtyDollarVisualizer.Engine.Asset_Management.Abstract;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Asset;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Types.Cache;

public class CachedAssetLoader : IAssetLoader<CachedAsset, CachedInfo>
{
    private const string CacheFolderName = "Cache";

    public bool Query(CachedInfo createInfo, AssetProvider assetProvider)
    {
        return assetProvider.Query<AssetStream, AssetInfo>(GenerateAssetInfoBasedOnCacheID(createInfo.CacheID));
    }

    public CachedAsset Load(CachedInfo createInfo, AssetProvider assetProvider,
        Func<CachedInfo, AssetProvider, CachedAsset> create)
    {
        return create(createInfo, assetProvider);
    }

    public CachedAsset Load(CachedInfo createInfo, AssetProvider assetProvider)
    {
        return Load(createInfo, assetProvider, Create);
    }

    public static CachedAsset Create(CachedInfo createInfo, AssetProvider assetProvider)
    {
        var assetStream = assetProvider.Load<AssetStream, AssetInfo>(
            GenerateAssetInfoBasedOnCacheID(createInfo.CacheID));

        return new CachedAsset
        {
            AssetStream = assetStream
        };
    }

    public static AssetInfo GenerateAssetInfoBasedOnCacheID(string cacheID)
    {
        var currentLocation = Environment.ProcessPath;
        ArgumentNullException.ThrowIfNull(currentLocation);

        var cacheFolder = Path.Combine(currentLocation, CacheFolderName);
        return new AssetInfo
        {
            Location = Path.Combine(cacheFolder, cacheID),
            Storage = StorageLocation.Disk
        };
    }
}
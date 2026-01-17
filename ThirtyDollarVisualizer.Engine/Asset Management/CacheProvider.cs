using System.Diagnostics.CodeAnalysis;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Cache;

namespace ThirtyDollarVisualizer.Engine.Asset_Management;

/// <summary>
/// A class that manages the caching of different objects.
/// </summary>
public class CacheProvider(AssetProvider assetProvider)
{
    private readonly Queue<(CachedInfo, byte[])> _cachedAssets = new();
    
    public bool TryLoadingCachedAsset(CachedInfo info, [NotNullWhen(true)] out CachedAsset? asset)
    {
        asset = null;
        if (!assetProvider.Query<CachedAsset, CachedInfo>(info)) return false;
        
        asset = assetProvider.Load<CachedAsset, CachedInfo>(info);
        return true;
    }

    public void EnqueueAssetToCache(CachedInfo info, byte[] assetData)
    {
        if (string.IsNullOrEmpty(info.CacheID))
            throw new ArgumentException("CacheID cannot be empty.", nameof(info));

        lock (_cachedAssets)
        {
            _cachedAssets.Enqueue((info, assetData));
        }
    }
    
    public void SaveQueuedAssets()
    {
        lock (_cachedAssets)
        {
            while (_cachedAssets.TryDequeue(out var tuple))
            {
                var (info, assetData) = tuple;
                var assetInfo = CachedAssetLoader.GenerateAssetInfoBasedOnCacheID(info.CacheID);

                File.WriteAllBytes(assetInfo.Location, assetData);
            }
        }
    }
}
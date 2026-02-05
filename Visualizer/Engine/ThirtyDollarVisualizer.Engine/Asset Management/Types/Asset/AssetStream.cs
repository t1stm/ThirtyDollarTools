using ThirtyDollarVisualizer.Engine.Asset_Management.Abstract;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Types.Asset;

public class AssetStream : ILoadableAsset<AssetStream, AssetInfo>
{
    public AssetInfo Info { get; set; } = new();
    public Stream Stream { get; set; } = Stream.Null;
    public static IAssetLoader<AssetStream, AssetInfo> AssetLoader { get; } = new AssetLoader();
}
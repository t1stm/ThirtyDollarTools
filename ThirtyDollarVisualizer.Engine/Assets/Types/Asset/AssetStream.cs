using ThirtyDollarVisualizer.Engine.Assets.Abstract;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Asset;

public class AssetStream : ILoadableAsset<AssetStream, AssetInfo>
{
    public static IAssetLoader<AssetStream, AssetInfo> AssetLoader { get; } = new AssetLoader();
    
    public AssetInfo Info { get; set; } = new();
    public Stream Stream { get; set; } = Stream.Null;
}
using Sundex.Engine.Asset_Management.Abstract.Metadata;

namespace Sundex.Engine.Asset_Management.Types.Asset;

public readonly struct AssetMetadata : IAssetMetadata<AssetMetadata, AssetInfo>
{
    public required DateTime ModifiedDate { get; init; }
    public required bool Found { get; init; }

    public static IMetadataLoader<AssetMetadata, AssetInfo> MetadataProvider { get; } = new AssetLoader();
}
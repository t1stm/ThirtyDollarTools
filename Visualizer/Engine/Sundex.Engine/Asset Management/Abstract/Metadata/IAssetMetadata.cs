namespace Sundex.Engine.Asset_Management.Abstract.Metadata;

public interface IAssetMetadata<out TMetadata, in TCreate> where TMetadata : allows ref struct
{
    /// <summary>
    /// Provides a mechanism to load or retrieve metadata for a specific type of asset.
    /// This abstract property is implemented in types conforming to the IAssetMetadata interface,
    /// facilitating the mapping between asset creation details and their corresponding metadata representations.
    /// </summary>
    /// <remarks>
    /// Implementers of this property typically return an instance of a class or struct that implements the
    /// IMetadataLoader interface, which is responsible for generating or loading metadata for assets.
    /// This enables a loosely coupled architecture for asset metadata management.
    /// </remarks>
    /// <typeparam name="TMetadata">
    /// The type of metadata associated with the asset. It must conform to the constraints of the IAssetMetadata implementation.
    /// </typeparam>
    /// <typeparam name="TCreate">
    /// The type of creation information used to generate or retrieve the associated metadata.
    /// </typeparam>
    public static abstract IMetadataLoader<TMetadata, TCreate> MetadataProvider { get; }
}
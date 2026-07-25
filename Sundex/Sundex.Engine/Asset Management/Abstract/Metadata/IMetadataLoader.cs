namespace Sundex.Engine.Asset_Management.Abstract.Metadata;

/// <summary>
///     Represents a contract for loading metadata for a specified creation type.
/// </summary>
/// <typeparam name="TMetadata">
///     The type of metadata to be returned.
/// </typeparam>
/// <typeparam name="TCreate">
///     The type of input used to retrieve or generate the metadata.
/// </typeparam>
public interface IMetadataLoader<out TMetadata, in TCreate> where TMetadata : allows ref struct
{
    /// <summary>
    ///     Retrieves metadata for the specified creation information.
    /// </summary>
    /// <param name="createInfo">
    ///     The information required to create or locate the metadata.
    /// </param>
    /// <returns>
    ///     An instance of <typeparamref name="TMetadata" /> containing metadata information for the specified creation
    ///     information.
    /// </returns>
    public TMetadata Metadata(TCreate createInfo);
}
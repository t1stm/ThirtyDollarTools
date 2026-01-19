using JetBrains.Annotations;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Abstract;

/// <summary>
///     Represents a loader for assets.
/// </summary>
/// <typeparam name="TReturn">The type to load.</typeparam>
/// <typeparam name="TCreate">The type used to load the <typeparamref name="TReturn" />.</typeparam>
public interface IAssetLoader<TReturn, TCreate>
{
    /// <summary>
    ///     Checks if the loader can load the specified asset.
    /// </summary>
    /// <param name="createInfo">The info to use when querying.</param>
    /// <param name="assetProvider">The asset provider instance responsible for managing and providing assets.</param>
    /// <returns>Whether the asset is loadable with this loader.</returns>
    bool Query(TCreate createInfo, AssetProvider assetProvider);

    /// <summary>
    ///     Loads an asset with a custom creation function.
    /// </summary>
    /// <param name="createInfo">The information required to create or load the asset.</param>
    /// <param name="assetProvider">The asset provider instance responsible for managing and providing assets.</param>
    /// <param name="create">A custom function to create the asset using the provided creation information and asset provider.</param>
    /// <returns>The loaded asset of the specified type.</returns>
    [UsedImplicitly]
    TReturn Load(TCreate createInfo, AssetProvider assetProvider, Func<TCreate, AssetProvider, TReturn> create);

    /// <summary>
    ///     Loads an asset using the default method.
    /// </summary>
    /// <param name="createInfo">The information required to create or load the asset.</param>
    /// <param name="assetProvider">The asset provider instance responsible for managing and providing assets.</param>
    /// <returns>The loaded asset of the specified type.</returns>
    TReturn Load(TCreate createInfo, AssetProvider assetProvider);

    /// <summary>
    ///     Creates an asset resource.
    /// </summary>
    /// <param name="createInfo">The information object that describes how the asset should be created or loaded.</param>
    /// <param name="assetProvider">The asset provider instance responsible for managing and supplying assets during creation.</param>
    /// <returns>The created asset resource of the specified type.</returns>
    [UsedImplicitly]
    public static abstract TReturn Create(TCreate createInfo, AssetProvider assetProvider);
}
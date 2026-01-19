namespace ThirtyDollarVisualizer.Engine.Asset_Management.Abstract;

/// <summary>
///     Represents a loadable asset.
/// </summary>
/// <typeparam name="TReturn">The type that is returned when loading.</typeparam>
/// <typeparam name="TCreate">The type to supply the asset loader with.</typeparam>
public interface ILoadableAsset<TReturn, TCreate>
{
    /// <summary>
    ///     The asset loader for this asset.
    /// </summary>
    public static abstract IAssetLoader<TReturn, TCreate> AssetLoader { get; }
}
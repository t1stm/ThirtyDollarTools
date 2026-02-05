using System.Reflection;
using Serilog.Core;
using ThirtyDollarVisualizer.Engine.Asset_Management.Abstract;
using ThirtyDollarVisualizer.Engine.Asset_Management.Helpers;
using ThirtyDollarVisualizer.Engine.Renderer.Queues;

namespace ThirtyDollarVisualizer.Engine.Asset_Management;

public class AssetProvider
{
    private readonly Logger _logger;

    public AssetProvider(Logger logger, Assembly[] assetAssemblies, GLInfo glInfo)
    {
        _logger = logger;
        AssetAssemblies = assetAssemblies;
        GLInfo = glInfo;
        ShaderPool = new ShaderPool(logger, this);
        CacheProvider = new CacheProvider(this);
    }

    public Assembly[] AssetAssemblies { get; }
    public ShaderPool ShaderPool { get; }
    public DeleteQueue DeleteQueue { get; } = new();
    public CacheProvider CacheProvider { get; }
    public GLInfo GLInfo { get; }

    /// <summary>
    ///     Checks if an asset can be loaded using the specified create info.
    /// </summary>
    /// <typeparam name="TReturn">
    ///     The type of asset to be queried, which implements
    ///     <see cref="ILoadableAsset&lt;TReturn, TCreate&gt;" />.
    /// </typeparam>
    /// <typeparam name="TCreate">The type of creation information required to query the asset loader.</typeparam>
    /// <param name="createInfo">The creation information used to query the asset loader.</param>
    /// <returns>A boolean indicating whether the specified asset can be loaded.</returns>
    public bool Query<TReturn, TCreate>(TCreate createInfo)
        where TReturn : ILoadableAsset<TReturn, TCreate>
    {
        return TReturn.AssetLoader.Query(createInfo, this);
    }

    /// <summary>
    ///     Loads an asset using the specified create info.
    /// </summary>
    /// <typeparam name="TReturn">
    ///     The type of asset to be loaded, which implements
    ///     <see cref="ILoadableAsset&lt;TReturn, TCreate&gt;" />.
    /// </typeparam>
    /// <typeparam name="TCreate">The type of creation information required to load the asset.</typeparam>
    /// <param name="createInfo">The information needed to create and load the asset.</param>
    /// <returns>The loaded asset of the specified type.</returns>
    public TReturn Load<TReturn, TCreate>(TCreate createInfo)
        where TReturn : ILoadableAsset<TReturn, TCreate>
    {
#if DEBUG
        _logger.Debug("[{ClassName}] Loading {ReturnType} with params: {@CreateInfo}", nameof(AssetProvider),
            typeof(TReturn).Name, createInfo);
#endif

        return TReturn.AssetLoader.Load(createInfo, this);
    }

    /// <summary>
    ///     Loads multiple assets using <see cref="Load&lt;TReturn, TCreate&gt;(TCreate)" />.
    /// </summary>
    /// <typeparam name="TReturn">
    ///     The type of the assets to be loaded, which implements
    ///     <see cref="ILoadableAsset&lt;TReturn, TCreate&gt;" />.
    /// </typeparam>
    /// <typeparam name="TCreate">The type of the creation information required to load the assets.</typeparam>
    /// <param name="destination">
    ///     The destination span where the loaded assets will be stored. Its length must be at least
    ///     equal to the number of provided creation information entries.
    /// </param>
    /// <param name="createInfos">
    ///     A collection of creation information entries that define how each corresponding asset should
    ///     be loaded.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when the length of the destination span is smaller than the number of
    ///     provided creation information entries.
    /// </exception>
    public void Load<TReturn, TCreate>(Span<TReturn> destination, params ReadOnlySpan<TCreate> createInfos)
        where TReturn : ILoadableAsset<TReturn, TCreate>
    {
        if (destination.Length < createInfos.Length)
            throw new ArgumentException("Destination span is too small for the given create infos.");

        for (var index = 0; index < createInfos.Length; index++)
        {
            var tCreate = createInfos[index];
            destination[index] = Load<TReturn, TCreate>(tCreate);
        }
    }

    /// <summary>
    ///     Loads multiple assets using <see cref="Load&lt;TReturn, TCreate&gt;(TCreate)" /> and returns them as an array.
    /// </summary>
    /// <param name="createInfos">The creation infos for each Load call.</param>
    /// <typeparam name="TReturn">
    ///     The type of the assets to be loaded, which implements
    ///     <see cref="ILoadableAsset&lt;TReturn, TCreate&gt;" />.
    /// </typeparam>
    /// <typeparam name="TCreate">The type of the creation information required to load the assets.</typeparam>
    /// <returns>An array of <typeparamref name="TReturn" />.</returns>
    public TReturn[] Load<TReturn, TCreate>(params ReadOnlySpan<TCreate> createInfos)
        where TReturn : ILoadableAsset<TReturn, TCreate>
    {
        var returnArray = new TReturn[createInfos.Length];
        Load(returnArray.AsSpan(), createInfos);
        return returnArray;
    }

    /// <summary>
    ///     Performs maintenance tasks like uploading shaders and executing deletes.
    /// </summary>
    public void Update()
    {
        ShaderPool.UploadShadersToPreload();
        DeleteQueue.ExecuteDeletes();
        CacheProvider.SaveQueuedAssets();
    }
}
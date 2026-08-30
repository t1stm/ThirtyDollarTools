using System.Reflection;
using Serilog;
using Sundex.Engine.Asset_Management.Abstract.Loading;
using Sundex.Engine.Asset_Management.Abstract.Metadata;
using Sundex.Engine.Asset_Management.Helpers;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Threading;

namespace Sundex.Engine.Asset_Management;

public class AssetProvider : IAssetProvider
{
    public AssetProvider(ILogger logger, Assembly[] assetAssemblies, GLInfo glInfo)
    {
        Logger = logger.ForContext<AssetProvider>();
        AssetAssemblies = assetAssemblies;
        SourceRoots = ReadSourceRoots(assetAssemblies);
        GLInfo = glInfo;
        ShaderPool = new ShaderPool(logger, this);
        CacheProvider = new CacheProvider(this);
        ThreadRunner = new ThreadRunner(logger);
    }

    public ILogger Logger { get; }

    public Assembly[] AssetAssemblies { get; }

    /// <summary>
    ///     Project directories of the asset assemblies, stamped in by Directory.Build.props
    ///     under Debug only. <see cref="Types.Asset.AssetLoader" /> probes these before the
    ///     output directory and the assembly manifest, so an asset load resolves to the file
    ///     on disk being edited and the UI can be reloaded without a rebuild. Empty in
    ///     Release, where the probe is skipped entirely.
    /// </summary>
    public string[] SourceRoots { get; }
    public ShaderPool ShaderPool { get; }
    public DeleteQueue DeleteQueue { get; } = new();
    public CacheProvider CacheProvider { get; }
    public GLInfo GLInfo { get; }

    /// <summary>
    ///     Runs work off the render thread with its exceptions still surfaced (see
    ///     <see cref="Sundex.Engine.Threading.ThreadRunner.Update" />), available to anything
    ///     holding an asset provider. <see cref="Game" /> exposes this same instance.
    /// </summary>
    public ThreadRunner ThreadRunner { get; }

    /// <summary>
    ///     Checks if an asset can be loaded using the specified create info.
    /// </summary>
    /// <typeparam name="TReturn">
    ///     The type of asset to be queried, which implements
    ///     <see cref="ILoadableAsset{TReturn,TCreate}" />.
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
        Logger.Debug("Loading {ReturnType} with params: {@CreateInfo}",
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
    ///     Retrieves metadata of the specified type for an asset based on the provided creation information.
    /// </summary>
    /// <typeparam name="TMetadata">
    ///     The type of metadata to retrieve, which must implement
    ///     <see cref="IAssetMetadata&lt;TMetadata, TCreate&gt;" />.
    /// </typeparam>
    /// <typeparam name="TCreate">
    ///     The type of the creation information required to query the metadata.
    /// </typeparam>
    /// <param name="createInfo">
    ///     The creation information used to retrieve the metadata for the asset.
    /// </param>
    /// <returns>
    ///     The retrieved metadata of type <typeparamref name="TMetadata" />.
    /// </returns>
    public TMetadata Metadata<TMetadata, TCreate>(TCreate createInfo)
        where TMetadata : IAssetMetadata<TMetadata, TCreate>, allows ref struct
    {
        return TMetadata.MetadataProvider.Metadata(createInfo);
    }

    /// <summary>
    ///     Reads the SundexSourceRoot metadata off each asset assembly. Assemblies without
    ///     it (Release builds, and anything not built from this repo) contribute nothing, and
    ///     directories that no longer exist are dropped once here rather than on every load.
    ///     ponytail: flat list, first hit wins, so two projects declaring the same
    ///     project-relative asset path would shadow each other. None do today; key the
    ///     probe by calling assembly if that changes.
    /// </summary>
    private static string[] ReadSourceRoots(Assembly[] assemblies)
    {
        return
        [
            ..assemblies
                .SelectMany(assembly => assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                .Where(attribute => attribute.Key == "SundexSourceRoot" && attribute.Value is not null)
                .Select(attribute => attribute.Value!)
                .Distinct(StringComparer.Ordinal)
                .Where(Directory.Exists)
        ];
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
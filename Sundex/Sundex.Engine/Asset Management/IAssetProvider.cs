using System.Reflection;
using Sundex.Engine.Asset_Management.Abstract.Loading;
using Sundex.Engine.Asset_Management.Abstract.Metadata;
using Sundex.Engine.Asset_Management.Helpers;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Threading;

namespace Sundex.Engine.Asset_Management;

/// <summary>
///     Abstraction over <see cref="AssetProvider" /> to allow mock implementations for testing.
/// </summary>
public interface IAssetProvider
{
    Assembly[] AssetAssemblies { get; }

    /// <summary>Project directories to resolve assets from before the assembly manifest; Debug only.</summary>
    string[] SourceRoots { get; }
    ShaderPool ShaderPool { get; }
    DeleteQueue DeleteQueue { get; }
    CacheProvider CacheProvider { get; }
    GLInfo GLInfo { get; }
    ThreadRunner ThreadRunner { get; }

    /// <summary>Checks if an asset can be loaded using the specified create info.</summary>
    bool Query<TReturn, TCreate>(TCreate createInfo)
        where TReturn : ILoadableAsset<TReturn, TCreate>;

    /// <summary>Loads an asset using the specified create info.</summary>
    TReturn Load<TReturn, TCreate>(TCreate createInfo)
        where TReturn : ILoadableAsset<TReturn, TCreate>;

    /// <summary>Loads multiple assets into a destination span.</summary>
    void Load<TReturn, TCreate>(Span<TReturn> destination, params ReadOnlySpan<TCreate> createInfos)
        where TReturn : ILoadableAsset<TReturn, TCreate>;

    /// <summary>Loads multiple assets and returns them as an array.</summary>
    TReturn[] Load<TReturn, TCreate>(params ReadOnlySpan<TCreate> createInfos)
        where TReturn : ILoadableAsset<TReturn, TCreate>;

    /// <summary>Retrieves metadata of type <typeparamref name="TMetadata" />.</summary>
    TMetadata Metadata<TMetadata, TCreate>(TCreate createInfo)
        where TMetadata : IAssetMetadata<TMetadata, TCreate>, allows ref struct;

    /// <summary>Performs maintenance tasks like uploading shaders and executing deletes.</summary>
    void Update();
}
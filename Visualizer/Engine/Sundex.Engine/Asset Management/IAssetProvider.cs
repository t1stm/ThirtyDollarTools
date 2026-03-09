using Sundex.Engine.Asset_Management.Abstract;
using Sundex.Engine.Asset_Management.Helpers;
using Sundex.Engine.Renderer.Queues;

namespace Sundex.Engine.Asset_Management;

/// <summary>
///     Abstraction over <see cref="AssetProvider" /> to allow mock implementations for testing.
/// </summary>
public interface IAssetProvider
{
    ShaderPool ShaderPool { get; }
    DeleteQueue DeleteQueue { get; }
    CacheProvider CacheProvider { get; }
    GLInfo GLInfo { get; }

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

    /// <summary>Performs maintenance tasks like uploading shaders and executing deletes.</summary>
    void Update();
}
using JetBrains.Annotations;
using ThirtyDollarVisualizer.Engine.Asset_Management;

namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract;

/// <summary>
/// Represents a contract for classes that require preloading of resources when the graphics context is created.
/// </summary>
public interface IGamePreloadable
{
    /// <summary>
    /// Called from the main render thread when the graphics context is created.
    /// Should preload necessary resources for the inheritor.
    /// </summary>
    /// <param name="assetProvider">The main AssetProvider instance.</param>
    [UsedImplicitly]
    static abstract void Preload(AssetProvider assetProvider);
}
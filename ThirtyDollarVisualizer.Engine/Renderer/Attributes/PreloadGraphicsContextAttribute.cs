using ThirtyDollarVisualizer.Engine.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Engine.Renderer.Attributes;

/// <summary>
/// Attribute that is queried at runtime to determine whether a class should be preloaded.
/// </summary>
/// <remarks>
/// The class must implement <see cref="IGamePreloadable"/>, otherwise a runtime exception will be thrown.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class PreloadGraphicsContextAttribute : Attribute;
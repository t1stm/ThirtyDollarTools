using OpenTK.Graphics.OpenGL;
using Serilog.Core;
using ThirtyDollarVisualizer.Engine.Asset_Management.Extensions;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Shader;
using ThirtyDollarVisualizer.Engine.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Helpers;

/// <summary>
/// A pool of shaders.
/// </summary>
public class ShaderPool(Logger logger, AssetProvider assetProvider)
{
    private readonly SemaphoreSlim _preloadLock = new(1, 1);
    private readonly List<(string shaderName, Func<AssetProvider, Shader> createFunction)> _shadersToPreload = [];
    private readonly Dictionary<string, Shader> _namedShaders = new();

    /// <summary>
    /// Gets or loads a shader with the specified location.
    /// When loading uses the shader name and appends both .vert and .frag to the name for loading.
    /// </summary>
    /// <param name="shaderLocation">The pure location of the shader without the extension. Assumes that the shaders are named with .vert and .frag</param>
    /// <returns>A shader object loaded from the specified location</returns>
    public Shader GetOrLoad(string shaderLocation)
    {
        return GetOrLoad(shaderLocation, provider => new Shader(assetProvider,
            provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader, shaderLocation + ".vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader, shaderLocation + ".frag")
            )
        ));
    }

    /// <summary>
    /// Retrieves a cached shader by name if available; otherwise, loads a new instance
    /// of the shader using the provided function and caches it.
    /// </summary>
    /// <param name="shaderLocation">
    /// The name of the shader to retrieve or load. This is used as the key
    /// for searching and storing shaders in the shader pool.
    /// </param>
    /// <param name="missingFunction">
    /// A function to create a new shader instance in case the shader with the given
    /// name is not found in the pool.
    /// </param>
    /// <returns>
    /// The shader instance retrieved from the pool or the newly loaded shader if it
    /// was not previously cached.
    /// </returns>
    public Shader GetOrLoad(string shaderLocation,
        Func<AssetProvider, Shader> missingFunction)
    {
#if DEBUG
        logger.Debug("[{ClassName}] Searching for shader with name: '{ShaderName}'", nameof(ShaderPool), shaderLocation);
#endif
        var alternative_lookup = _namedShaders.GetAlternateLookup<ReadOnlySpan<char>>();
        if (alternative_lookup.TryGetValue(shaderLocation, out var shader))
            return shader;

#if DEBUG
        logger.Debug("[{ClassName}] Shader with name: '{ShaderName}' not found, invoking load function.",
            nameof(ShaderPool), shaderLocation);
#endif

        var result = missingFunction.Invoke(assetProvider);
        alternative_lookup[shaderLocation] = result;
        return result;
    }

    /// <summary>
    /// Adds the specified shader to a preload queue that executes on each draw call where OpenGL is currently bound.
    /// </summary>
    /// <param name="shaderName">The name of the shader to preload.</param>
    /// <param name="function">Function to create a new shader instance.</param>
    public void PreloadShader(string shaderName, Func<AssetProvider, Shader> function)
    {
        _preloadLock.Wait();
        try
        {
            _shadersToPreload.Add((shaderName, function));
        }
        finally
        {
            _preloadLock.Release();
        }
    }

    /// <summary>
    /// Retrieves a named shader from the shader pool. If the shader is not found,
    /// an exception is thrown.
    /// </summary>
    /// <param name="shaderName">
    /// The name of the shader to retrieve. This is used as the key for
    /// searching shaders in the shader pool.
    /// </param>
    /// <returns>
    /// The shader instance associated with the specified name.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the shader with the specified name is not found in the pool.
    /// </exception>
    public Shader GetNamedShader(ReadOnlySpan<char> shaderName)
    {
        var alternative_lookup = _namedShaders.GetAlternateLookup<ReadOnlySpan<char>>();
        alternative_lookup.TryGetValue(shaderName, out var shader);
        ArgumentNullException.ThrowIfNull(shader);
        return shader;
    }

    /// <summary>
    /// Reloads all shaders currently cached in the shader pool. This involves re-compiling
    /// and re-initializing the shaders to ensure they are up to date and synchronized
    /// with any changes in their definitions or associated resources.
    /// </summary>
    public void Reload()
    {
        foreach (var shader in _namedShaders.Values)
        {
            shader.ReloadShader();
        }
    }

    public void UploadShadersToPreload()
    {
        var lookup = _namedShaders.GetAlternateLookup<ReadOnlySpan<char>>();
        _preloadLock.Wait();

        foreach (var (shaderName, createFunction) in _shadersToPreload)
        {
            if (lookup.ContainsKey(shaderName))
                continue;

            var result = createFunction.Invoke(assetProvider);
            lookup[shaderName] = result;
        }

        _preloadLock.Release();
    }
}
namespace ThirtyDollarVisualizer.Renderer.Shaders;

/// <summary>
/// A pool of shaders.
/// </summary>
public static class ShaderPool
{
    private static readonly Dictionary<string, Shader> NamedShaders = new();

    /// <summary>
    /// Retrieves a cached shader by name if available; otherwise, loads a new instance
    /// of the shader using the provided function and caches it.
    /// </summary>
    /// <param name="shaderName">
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
    public static Shader GetOrLoad(ReadOnlySpan<char> shaderName, Func<Shader> missingFunction)
    {
        var alternative_lookup = NamedShaders.GetAlternateLookup<ReadOnlySpan<char>>();
        if (alternative_lookup.TryGetValue(shaderName, out var shader))
            return shader;

        var result = missingFunction.Invoke();
        alternative_lookup[shaderName] = result;
        return result;
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
    public static Shader GetNamedShader(ReadOnlySpan<char> shaderName)
    {
        var alternative_lookup = NamedShaders.GetAlternateLookup<ReadOnlySpan<char>>();
        alternative_lookup.TryGetValue(shaderName, out var shader);
        ArgumentNullException.ThrowIfNull(shader);
        return shader;
    }

    /// <summary>
    /// Reloads all shaders currently cached in the shader pool. This involves re-compiling
    /// and re-initializing the shaders to ensure they are up to date and synchronized
    /// with any changes in their definitions or associated resources.
    /// </summary>
    public static void Reload()
    {
        foreach (var shader in NamedShaders.Values)
        {
            shader.ReloadShader();
        }
    }
}
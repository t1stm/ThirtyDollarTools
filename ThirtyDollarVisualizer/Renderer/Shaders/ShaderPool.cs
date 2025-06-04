namespace ThirtyDollarVisualizer.Renderer.Shaders;

public static class ShaderPool
{
    private static readonly Dictionary<string, Shader> NamedShaders = new();

    public static Shader GetOrLoad(ReadOnlySpan<char> shaderName, Func<Shader> missingFunction)
    {
        var alternative_lookup = NamedShaders.GetAlternateLookup<ReadOnlySpan<char>>();
        if (alternative_lookup.TryGetValue(shaderName, out var shader))
            return shader;

        var result = missingFunction.Invoke();
        alternative_lookup[shaderName] = result;
        return result;
    }

    public static Shader GetNamedShader(ReadOnlySpan<char> shaderName)
    {
        var alternative_lookup = NamedShaders.GetAlternateLookup<ReadOnlySpan<char>>();
        alternative_lookup.TryGetValue(shaderName, out var shader);
        ArgumentNullException.ThrowIfNull(shader);
        return shader;
    }
}
namespace ThirtyDollarVisualizer.Renderer.Shaders;

public static class ShaderPool
{
    private static readonly Dictionary<string, Shader> NamedShaders = new();

    public static Shader GetOrLoad(ReadOnlySpan<char> shader_name, Func<Shader> missing_function)
    {
        var alternative_lookup = NamedShaders.GetAlternateLookup<ReadOnlySpan<char>>();
        if (alternative_lookup.TryGetValue(shader_name, out var shader))
            return shader;

        var result = missing_function.Invoke();
        alternative_lookup[shader_name] = result;
        return result;
    }

    public static Shader GetNamedShader(ReadOnlySpan<char> shader_name)
    {
        var alternative_lookup = NamedShaders.GetAlternateLookup<ReadOnlySpan<char>>();
        alternative_lookup.TryGetValue(shader_name, out var shader);
        ArgumentNullException.ThrowIfNull(shader);
        return shader;
    }
}
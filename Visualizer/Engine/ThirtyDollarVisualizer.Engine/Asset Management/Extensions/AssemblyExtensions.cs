using System.Reflection;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Extensions;

public static class AssemblyExtensions
{
    public static ManifestResourceInfo? GetManifestResourceInfo(this Assembly[] assemblies, ReadOnlySpan<char> name)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var assembly in assemblies)
        {
            var info = assembly.GetManifestResourceInfo(name.ToString());
            if (info != null) return info;
        }

        return null;
    }

    public static Stream? GetManifestResourceStream(this Assembly[] assemblies, ReadOnlySpan<char> name)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var assembly in assemblies)
        {
            var assemblyName = assembly.GetName().Name;
            var info = assembly.GetManifestResourceStream($"{assemblyName}.{name}");
            if (info != null) return info;
        }

        return null;
    }
}
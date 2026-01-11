using System.Reflection;

namespace ThirtyDollarVisualizer.Engine.Assets.Extensions;

public static class AssemblyExtensions
{
    public static ManifestResourceInfo? GetManifestResourceInfo(this Assembly[] assemblies, string name)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var assembly in assemblies)
        {
            var info = assembly.GetManifestResourceInfo(name);
            if (info != null) return info;
        }

        return null;
    }
    
    public static Stream? GetManifestResourceStream(this Assembly[] assemblies, string name)
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
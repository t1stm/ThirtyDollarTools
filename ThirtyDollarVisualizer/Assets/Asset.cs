using System.Reflection;

namespace ThirtyDollarVisualizer.Assets;

public static class Asset
{
    /// <summary>
    /// Returns the path of an embedded asset.
    /// </summary>
    /// <param name="assetPath">The path of the asset as seen in the source code.</param>
    /// <returns>The embedded path if on release, and the copied path to the build directory when in debug.</returns>
    public static string Embedded(string assetPath)
    {
        var location = "Assets/" + assetPath;
        
        #if DEBUG
            return File.Exists(location) ? location : 
                throw new FileNotFoundException($"Unable to find asset '{location}' in real path. " +
                                                $"Maybe you forgot to copy it to the build directory? " +
                                                $"(This step should be done automatically by the build " +
                                                $"if the file was configured correctly in the .csproj)");
        #endif
        
        var assembly = Assembly.GetExecutingAssembly().GetName().Name;
        return $"{assembly}.{location.Replace('/', '.')}";
    }
}
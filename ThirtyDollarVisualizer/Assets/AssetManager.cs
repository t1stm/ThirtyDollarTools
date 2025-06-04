using System.Reflection;
using ThirtyDollarVisualizer.Helpers.Logging;

namespace ThirtyDollarVisualizer.Assets;

public static class AssetManager
{
    public static AssetDefinition GetAsset(string path)
    {
        Stream source;
        var isEmbedded = false;

#if DEBUG
        DefaultLogger.Log("AssetManager", $"Loading asset '{path}'");
#endif

        if (path.Contains('*'))
        {
            source = LoadWildcard(path);
        }
        else if (File.Exists(path))
        {
            source = File.OpenRead(path);
        }
        else
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            source = stream ??
                     throw new FileNotFoundException($"Unable to find asset '{path}' in assembly or real path.");
            isEmbedded = true;
        }

        return new AssetDefinition
        {
            Stream = source,
            IsEmbedded = isEmbedded
        };
    }

    private static FileStream LoadWildcard(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory)) directory = Directory.GetCurrentDirectory();
        
        var search_pattern = Path.GetFileName(path);
        if (string.IsNullOrEmpty(search_pattern))
            throw new ArgumentException("Invalid pattern: No file name specified.", nameof(path));

        var files = Directory.GetFiles(directory, search_pattern);
        if (files.Length == 0) throw new FileNotFoundException($"Unable to find any files matching '{path}'.");
        return File.OpenRead(files[0]);
    }
}
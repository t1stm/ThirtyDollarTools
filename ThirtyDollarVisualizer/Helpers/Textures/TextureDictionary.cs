using System.Collections.Concurrent;
using System.Reflection;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Textures;

namespace ThirtyDollarVisualizer.Helpers.Textures;

public static class TextureDictionary
{
    private static AssetTexture? MissingTexture;
    private static AssetTexture? ICutTexture;

    private static readonly ConcurrentDictionary<string, AssetTexture> Dictionary = new();

    public static void Clear()
    {
        Dictionary.Clear();
    }

    private static bool Exists(string path)
    {
        if (!path.Contains('*'))
            return File.Exists(path) ||
                   Assembly.GetExecutingAssembly().GetManifestResourceInfo(path) is not null;
        
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        var searchPattern = Path.GetFileName(path);
        if (string.IsNullOrEmpty(searchPattern))
        {
            throw new ArgumentException("Invalid pattern; no file name specified.", nameof(path));
        }

        var files = Directory.GetFiles(directory, searchPattern);
        return files.Length > 0;
    }

    private static AssetTexture LoadAsset(string path)
    {
        return new AssetTexture(path);
    }

    public static AssetTexture? GetAsset(string path)
    {
        return Exists(path) ? Dictionary.GetOrAdd(path, LoadAsset) : null;
    }

    public static AssetTexture? GetDownloadedAsset(string location, string name)
    {
        var image = $"{location}/Images/" + name.Replace("!", "action_") + ".*";
        return GetAsset(image);
    }

    public static AssetTexture GetMissingTexture()
    {
        return MissingTexture ??=
            GetAsset("ThirtyDollarVisualizer.Assets.Textures.action_missing.png") ??
            throw new Exception("The missing event texture is missing in the assembly.");
    }

    public static AssetTexture GetICutEventTexture()
    {
        return ICutTexture ??=
            GetAsset("ThirtyDollarVisualizer.Assets.Textures.action_icut.png") ??
            throw new Exception("The #icut event texture is missing in the assembly.");
    }
}
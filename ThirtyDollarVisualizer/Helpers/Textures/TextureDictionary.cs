using System.Collections.Concurrent;
using System.Reflection;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Helpers.Textures;

public static class TextureDictionary
{
    private static Texture? MissingTexture;
    private static Texture? ICutTexture;

    private static readonly ConcurrentDictionary<string, Texture> Dictionary = new();

    public static void Clear()
    {
        Dictionary.Clear();
    }

    private static bool Exists(string path)
    {
        return File.Exists(path) || Assembly.GetExecutingAssembly().GetManifestResourceInfo(path) is not null;
    }

    private static Texture LoadAsset(string path)
    {
        if (!Exists(path)) throw new FileNotFoundException($"Asset with location: '{path}' not found.");
        return new Texture(path);
    }

    public static Texture? GetAsset(string path)
    {
        return Exists(path) ? Dictionary.GetOrAdd(path, LoadAsset) : null;
    }

    public static Texture? GetDownloadedAsset(string location, string name)
    {
        var image = $"{location}/Images/" + name.Replace("!", "action_") + ".png";
        return GetAsset(image);
    }

    public static Texture GetMissingTexture()
    {
        return MissingTexture ??=
            GetAsset("ThirtyDollarVisualizer.Assets.Textures.action_missing.png") ??
            throw new Exception("The missing event texture is missing in the assembly.");
    }

    public static Texture GetICutEventTexture()
    {
        return ICutTexture ??=
            GetAsset("ThirtyDollarVisualizer.Assets.Textures.action_icut.png") ??
            throw new Exception("The #icut event texture is missing in the assembly.");
    }
}
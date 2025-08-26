using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;
using ThirtyDollarVisualizer.Helpers.Textures;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

public class StaticSoundAtlas : ImageAtlas
{
    private readonly Dictionary<string, QuadUV> _coordinateTable = new();
    public Vector2 Size => new(Width, Height); 

    public static StaticSoundAtlas FromFiles(string downloadLocation, IEnumerable<string> soundFiles, out Dictionary<string, AssetTexture> animatedTextures)
    {
        var atlas = new StaticSoundAtlas();
        animatedTextures = new Dictionary<string, AssetTexture>();

        foreach (var soundFile in soundFiles)
        {
            var texture = TextureDictionary.GetDownloadedAsset(downloadLocation, soundFile);
            if (texture == null)
                continue;
            
            if (texture.IsAnimated)
            {
                animatedTextures.Add(soundFile, texture);
                continue;
            }
            
            var staticTexture = texture.Texture.As<StaticTexture>();
            var image = staticTexture.GetData();
            
            if (image == null)
                throw new Exception("Failed to get image data from texture.");
            
            var coords = atlas.AddImage(image.Frames.RootFrame);
            atlas._coordinateTable.Add(soundFile, coords.ToUV(atlas.Size));
        }

        return atlas;
    }

    public QuadUV GetSoundUV(string soundName)
    {
        return GetSoundUV(soundName.AsSpan()); 
    }

    public QuadUV GetSoundUV(ReadOnlySpan<char> soundName)
    {
        var alternativeLookup = _coordinateTable.GetAlternateLookup<ReadOnlySpan<char>>();
        alternativeLookup.TryGetValue(soundName, out var uv);
        return uv;
    }
}
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;
using ThirtyDollarVisualizer.Helpers.Textures;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

public class StaticSoundAtlas : ImageAtlas
{
    private readonly Dictionary<string, AtlasReference> _coordinateTable = new();

    public static List<StaticSoundAtlas> FromFiles(string downloadLocation, IEnumerable<string> soundFiles,
        out Dictionary<string, AssetTexture> animatedTextures)
    {
        animatedTextures = new Dictionary<string, AssetTexture>();
        List<StaticSoundAtlas> list = [new()];

        var currentAtlas = list[0];
        foreach (var soundFile in soundFiles)
        {
            var texture = TextureDictionary.GetDownloadedAsset(downloadLocation, soundFile);
            if (texture == null)
                continue;

            AddTextureToAtlas(
                texture,
                soundFile,
                animatedTextures,
                currentAtlas,
                list);
        }

        AddTextureToAtlas(
            TextureDictionary.GetICutEventTexture(),
            "#icut",
            animatedTextures,
            currentAtlas,
            list);

        AddTextureToAtlas(
            TextureDictionary.GetMissingTexture(),
            "#missing",
            animatedTextures,
            currentAtlas,
            list
        );

        return list;
    }

    private static void AddTextureToAtlas(AssetTexture texture, string sound,
        Dictionary<string, AssetTexture> animatedTextures, StaticSoundAtlas currentAtlas, List<StaticSoundAtlas> list)
    {
        if (texture.IsAnimated)
        {
            animatedTextures.Add(sound, texture);
            return;
        }

        var staticTexture = texture.Texture.As<StaticTexture>();
        var image = staticTexture.GetData();

        if (image == null)
            throw new Exception("Failed to get image data from texture.");

        if (!currentAtlas.Atlas.CanFit(image.Frames.RootFrame.Width, image.Frames.RootFrame.Height))
            list.Add(currentAtlas = new StaticSoundAtlas());

        if (sound.StartsWith("action_"))
            sound = "!" + sound[7..];

        var reference = currentAtlas.AddImage(sound, image.Frames.RootFrame);
        currentAtlas._coordinateTable.Add(sound, reference);
    }

    public QuadUV GetSoundUV(string soundName)
    {
        return GetSoundUV(soundName.AsSpan());
    }

    public QuadUV GetSoundUV(ReadOnlySpan<char> soundName)
    {
        var found = TryGetSound(soundName, out var reference);
        return !found ? new QuadUV() : reference.TextureUV;
    }

    public bool TryGetSound(ReadOnlySpan<char> soundName, out AtlasReference reference)
    {
        var alternativeLookup = _coordinateTable.GetAlternateLookup<ReadOnlySpan<char>>();
        return alternativeLookup.TryGetValue(soundName, out reference);
    }
}
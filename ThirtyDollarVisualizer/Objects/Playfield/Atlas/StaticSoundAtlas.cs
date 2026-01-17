using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using ThirtyDollarVisualizer.Engine.Assets;
using ThirtyDollarVisualizer.Engine.Assets.Types.Asset;
using ThirtyDollarVisualizer.Engine.Assets.Types.Texture;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Renderer.Textures.Atlases;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

[PreloadGraphicsContext]
public class StaticSoundAtlas(int width, int height, InternalFormat internalFormat = InternalFormat.Rgba8)
    : GPUTextureAtlas(width, height, internalFormat), IGamePreloadable
{
    private static AssetProvider _assetProvider = null!;
    private static int _instanceID;

    public static void Preload(AssetProvider assetProvider)
    {
        _assetProvider = assetProvider;
    }

    private readonly Dictionary<string, Rectangle> _coordinateTable = new();

    public static List<StaticSoundAtlas> FromFiles(string downloadLocation, IEnumerable<string> soundFiles,
        out Dictionary<string, TextureHolder> animatedTextures)
    {
        const int defaultSize = 4096;
        
        animatedTextures = new Dictionary<string, TextureHolder>();
        List<StaticSoundAtlas> list = [new(defaultSize, defaultSize)
        {
            AtlasID = "SoundsAtlas_" + _instanceID++
        }];

        var currentAtlas = list[0];
        foreach (var soundFile in soundFiles)
        {
            var textureInfo = new TextureInfo
            {
                AssetInfo = new AssetInfo
                {
                    Location = $"{downloadLocation}/{soundFile}.*",
                    Storage = StorageLocation.Disk
                }
            };

            if (!_assetProvider.Query<TextureHolder, TextureInfo>(textureInfo))
                throw new Exception($"Unable to find texture for file: {soundFile}");

            var texture = _assetProvider.Load<TextureHolder, TextureInfo>(textureInfo);
            AddTextureToAtlas(
                texture,
                soundFile,
                animatedTextures,
                currentAtlas,
                list);
        }

        AddTextureToAtlas(
            _assetProvider.Load<TextureHolder, TextureInfo>(new TextureInfo
            {
                AssetInfo = new AssetInfo
                {
                    Location = $"Assets/Textures/action_icut.png",
                    Storage = StorageLocation.Assembly
                }
            }),
            "#icut",
            animatedTextures,
            currentAtlas,
            list);

        AddTextureToAtlas(
            _assetProvider.Load<TextureHolder, TextureInfo>(new TextureInfo
            {
                AssetInfo = new AssetInfo
                {
                    Location = $"Assets/Textures/action_missing.png",
                    Storage = StorageLocation.Assembly
                }
            }),
            "#missing",
            animatedTextures,
            currentAtlas,
            list
        );

        return list;
    }

    private static void AddTextureToAtlas(TextureHolder texture, string sound,
        Dictionary<string, TextureHolder> animatedTextures, StaticSoundAtlas currentAtlas, List<StaticSoundAtlas> list)
    {
        if (texture.TextureInfo.IsAnimated)
        {
            animatedTextures.Add(sound, texture);
            return;
        }

        var image = texture.Texture.Frames.RootFrame;
        if (image == null)
            throw new Exception("Failed to get image data from texture.");


        if (!currentAtlas.Atlas.CanFit(image.Width, image.Height))
            list.Add(currentAtlas =
                new StaticSoundAtlas(currentAtlas.Width, currentAtlas.Height, currentAtlas.Texture.InternalFormat)
                {
                    AtlasID = "SoundsAtlas_" + _instanceID++
                });

        if (sound.StartsWith("action_"))
            sound = "!" + sound[7..];

        currentAtlas.AddTexture(sound, image);
        var reference = currentAtlas.Atlas.GetImageRectangle(sound);

        currentAtlas._coordinateTable.Add(sound, reference);
    }

    public Rectangle GetSoundUV(string soundName)
    {
        return GetSoundUV(soundName.AsSpan());
    }

    public Rectangle GetSoundUV(ReadOnlySpan<char> soundName)
    {
        var found = TryGetSound(soundName, out var reference);
        return !found ? new Rectangle() : reference;
    }

    public bool TryGetSound(ReadOnlySpan<char> soundName, out Rectangle reference)
    {
        var alternativeLookup = _coordinateTable.GetAlternateLookup<ReadOnlySpan<char>>();
        return alternativeLookup.TryGetValue(soundName, out reference);
    }
}
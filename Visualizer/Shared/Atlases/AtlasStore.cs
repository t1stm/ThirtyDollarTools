using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.Texture;
using Sundex.Engine.Renderer.Textures.Atlases;

namespace Shared.Atlases;

public class AtlasStore(AssetProvider assetProvider, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<AtlasStore>();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    ///     Generic container for static sound atlases.
    /// </summary>
    public List<StaticSoundAtlas> StaticAtlases { get; } = [];

    /// <summary>
    ///     Map Sound -> FramedAtlas.
    /// </summary>
    public Dictionary<string, FramedAtlas> AnimatedSounds { get; } = [];

    /// <summary>
    ///     Map Sound -> StaticSoundAtlas.
    /// </summary>
    public Dictionary<string, StaticSoundAtlas> StaticSounds { get; } = [];

    public void Update()
    {
        _semaphore.Wait();
        try
        {
            foreach (var (_, atlas) in AnimatedSounds) atlas.Update();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void LoadImageAtPath(string imagePath, string soundName,
        StorageLocation storageLocation = StorageLocation.Disk)
    {
        var assetInfo = new AssetInfo
        {
            Location = imagePath,
            Storage = storageLocation
        };

        var textureInfo = new TextureInfo
        {
            AssetInfo = assetInfo
        };

        if (!assetProvider.Query<AssetStream, AssetInfo>(assetInfo))
        {
            _logger.Warning("Image file not found at path: {ImagePath}. Skipping.", imagePath);
            return;
        }

        var textureHolder = assetProvider.Load<TextureHolder, TextureInfo>(textureInfo);
        _semaphore.Wait();
        try
        {
            if (textureHolder.TextureInfo.IsAnimated)
                HandleAnimatedImage(textureHolder, soundName);
            else
                HandleStaticImage(textureHolder, soundName);
        }
        finally
        {
            _semaphore.Release();
        }

        _logger.Debug("Loaded asset {ImagePath} with {FrameCount} frames", imagePath,
            textureHolder.Texture.Frames.Count);
    }

    private void HandleAnimatedImage(TextureHolder holder, string soundName)
    {
        var lookup = AnimatedSounds.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(soundName, out var atlas)) return;

        atlas = FramedAtlas.FromAnimatedTexture(soundName, holder);
        AnimatedSounds.Add(soundName, atlas);
    }

    private void HandleStaticImage(TextureHolder holder, string soundName)
    {
        var image = holder.Texture;

        if (image.Frames.Count == 0)
            throw new Exception("Image has no frames.");

        var frame = image.Frames[0];

        var atlas = StaticAtlases.FirstOrDefault(soundAtlas => soundAtlas.Atlas.CanFit(frame.Width, frame.Height));
        if (atlas == null)
            StaticAtlases.Add(atlas = new StaticSoundAtlas(4096, 4096)
            {
                AtlasID = $"StaticAtlas_{StaticAtlases.Count}"
            });

        StaticSounds[soundName] = atlas;
        AddImageFrameToAtlas(frame, soundName, atlas);
    }

    private static void AddImageFrameToAtlas(ImageFrame<Rgba32> frame, string frameName, GPUTextureAtlas atlas)
    {
        atlas.AddTexture(frameName, frame);
    }
}
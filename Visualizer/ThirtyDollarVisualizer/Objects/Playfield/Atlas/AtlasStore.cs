using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Asset;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

public class AtlasStore(AssetProvider assetProvider)
{
    /// <summary>
    /// Generic container for static sound atlases.
    /// </summary>
    public List<StaticSoundAtlas> StaticAtlases { get; } = [];
    
    /// <summary>
    /// Map Sound -> FramedAtlas.
    /// </summary>
    public Dictionary<string, FramedAtlas> AnimatedSounds { get; } = [];
    
    /// <summary>
    /// Map Sound -> StaticSoundAtlas.
    /// </summary>
    public Dictionary<string, StaticSoundAtlas> StaticSounds { get; } = [];

    public void Update()
    {
        foreach (var (_, atlas) in AnimatedSounds) atlas.Update();
    }

    public void LoadImageAtPath(string imagePath, string soundName)
    {
        var assetInfo = new AssetInfo
        {
            Location = imagePath,
            Storage = StorageLocation.Disk
        };
            
        if (!assetProvider.Query<AssetStream, AssetInfo>(assetInfo))
            throw new FileNotFoundException($"Image file not found at path: {imagePath}");
        
        var assetStream = assetProvider.Load<AssetStream, AssetInfo>(assetInfo);
        var image = Image.Load<Rgba32>(assetStream.Stream);
        
        if (image.Frames.Count > 1)
            HandleAnimatedImage(image, soundName);
        else
            HandleStaticImage(image, soundName);
    }

    private void HandleAnimatedImage(Image<Rgba32> image, string soundName)
    {
        
    }
    
    private void HandleStaticImage(Image<Rgba32> image, string soundName)
    {
        
    }
}
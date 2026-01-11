using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Engine.Assets.Abstract;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Texture;

public class TextureHolder : ILoadableAsset<TextureHolder, TextureInfo>
{
    public static IAssetLoader<TextureHolder, TextureInfo> AssetLoader { get; } = new TextureLoader();
    
    public required TextureInfo TextureInfo { get; set; } = new();
    public required Image<Rgba32> Texture { get; set; }
}
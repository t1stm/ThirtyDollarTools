using ThirtyDollarVisualizer.Engine.Assets.Abstract;
using ThirtyDollarVisualizer.Engine.Assets.Types.Asset;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Texture;

public class TextureInfo : ILoaderInfo
{
    public AssetInfo AssetInfo { get; set; } = new();
    
    public bool IsAnimated { get; set; }
    
    /// <summary>
    /// Width of the texture in pixels.
    /// Setting this as a load parameter will resize the texture to the given dimensions.
    /// If unset/equal to 0, this will be updated when the texture is loaded.
    /// </summary>
    public int Width { get; set; }
    
    /// <summary>
    /// Height of the texture in pixels.
    /// Setting this as a load parameter will resize the texture to the given dimensions.
    /// If unset/equal to 0, this will be updated when the texture is loaded.
    /// </summary>
    public int Height { get; set; }
}
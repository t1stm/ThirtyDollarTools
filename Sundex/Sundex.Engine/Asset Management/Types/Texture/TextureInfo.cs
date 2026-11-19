using Sundex.Engine.Asset_Management.Abstract.Loading;
using Sundex.Engine.Asset_Management.Types.Asset;

namespace Sundex.Engine.Asset_Management.Types.Texture;

public class TextureInfo : ILoaderInfo
{
    public AssetInfo AssetInfo { get; set; } = new();

    public bool IsAnimated { get; set; }

    /// <summary>
    ///     Width of the texture in pixels.
    ///     Setting this as a load parameter will resize the texture to the given dimensions.
    ///     If unset/equal to 0, this will be updated when the texture is loaded.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Height of the texture in pixels.
    ///     Setting this as a load parameter will resize the texture to the given dimensions.
    ///     If unset/equal to 0, this will be updated when the texture is loaded.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     The widest the decoded pixels may be, in pixels. Unlike <see cref="Width" /> this only
    ///     ever shrinks, keeping the aspect ratio: an image already narrower stays as it is.
    /// </summary>
    public int MaxWidth { get; init; }

    /// <summary>
    ///     A Gaussian blur, as a sigma in pixels; 0 for none. In the pixels of the box the image is
    ///     shown in when <see cref="BlurBoxWidth" /> and <see cref="BlurBoxHeight" /> are set (CSS
    ///     <c>filter: blur()</c>), else in the decoded pixels.
    /// </summary>
    public float Blur { get; init; }

    /// <summary>The width of the box the blurred image fills, in pixels; 0 if unknown.</summary>
    public float BlurBoxWidth { get; init; }

    /// <summary>The height of the box the blurred image fills, in pixels; 0 if unknown.</summary>
    public float BlurBoxHeight { get; init; }
}
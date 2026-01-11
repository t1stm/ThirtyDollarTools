using System.Text.Json;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Engine.Assets;
using ThirtyDollarVisualizer.Engine.Assets.Types.Cache;
using ThirtyDollarVisualizer.Engine.Common;

namespace ThirtyDollarVisualizer.Engine.Renderer.Textures.Atlases;

public class GPUTextureAtlas(int width, int height, InternalFormat internalFormat = InternalFormat.Rgba8)
{
    public required string AtlasID { get; init; } // used for identification of the atlas when caching
    public int Width { get; } = width;
    public int Height { get; } = height;

    public GPUTexture Texture { get; set; } = new()
    {
        Width = width,
        Height = height,
        InternalFormat = internalFormat
    };

    public GuillotineAtlas Atlas { get; set; } = new(width, height);

    public void AddTexture<TPixel>(string name, ImageFrame<TPixel> image) where TPixel : unmanaged, IPixel, IPixel<TPixel>
    {
        var rectangle = Atlas.AddImage(name, image);
        Texture.QueueUploadToGPU(image, rectangle);
    }
    
    public void Bind() => Texture.Bind();

    public void LoadFromCache(AssetProvider assetProvider)
    {
        var textureExists = assetProvider.CacheProvider.TryLoadingCachedAsset(new CachedInfo
        {
            CacheID = "Atlas_Texture" + AtlasID
        }, out var atlasTexture);
        
        var atlasInfoExists = assetProvider.CacheProvider.TryLoadingCachedAsset(new CachedInfo
        {
            CacheID = "Atlas_Lookup" + AtlasID
        }, out var atlasInfo);

        if (!(textureExists && atlasInfoExists))
            return;

        if (internalFormat != InternalFormat.Rgba8 && internalFormat != InternalFormat.Rgba32f)
            throw new Exception("Invalid atlas texture format for saving and loading.");
        
        switch (internalFormat)
        {
            case InternalFormat.Rgba8:
            {
                var texture = Image.Load<Rgba32>(atlasTexture!.AssetStream.Stream);
                Texture.QueueUploadToGPU(texture.Frames.RootFrame);
                break;
            }

            case InternalFormat.Rgba32f:
            {
                var texture = Image.Load<RgbaVector>(atlasTexture!.AssetStream.Stream);
                Texture.QueueUploadToGPU(texture.Frames.RootFrame);
                break;
            }
        }
        
        var overrideAtlas = JsonSerializer.Deserialize<GuillotineAtlas>(atlasInfo!.AssetStream.Stream, SerializerOptions.Json);
        if (overrideAtlas == null) return;
        
        Atlas = overrideAtlas;
    }
}
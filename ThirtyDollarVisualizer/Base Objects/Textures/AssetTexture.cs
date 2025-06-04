using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Assets;
using ThirtyDollarVisualizer.Objects.Textures.Animated;
using ThirtyDollarVisualizer.Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Objects.Textures;

public class AssetTexture : SingleTexture
{
    private readonly SingleTexture Texture;
    
    public AssetTexture(string path)
    {
        var asset = AssetManager.GetAsset(path);
        using var source = asset.Stream;
        
        var image = Image.Load<Rgba32>(source);
        Width = image.Width;
        Height = image.Height;

        if (image.Frames.Count > 1)
            Texture = new AnimatedTexture(image);
        else Texture = new StaticTexture(image);
    }

    public bool IsAnimated => Texture is AnimatedTexture;
    
    public override bool NeedsUploading()
    {
        return Texture.NeedsUploading();
    }

    public override void Update()
    {
        Texture.Update();
    }

    public override void UploadToGPU()
    {
        Texture.UploadToGPU();
    }

    public override void Bind(TextureUnit slot = TextureUnit.Texture0)
    {
        Texture.Bind(slot);
    }

    public override void Dispose()
    {
        Texture.Dispose();
        GC.SuppressFinalize(this);
    }
}
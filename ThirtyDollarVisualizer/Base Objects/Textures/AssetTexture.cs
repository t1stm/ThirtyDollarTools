using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ThirtyDollarVisualizer.Assets;
using ThirtyDollarVisualizer.Base_Objects.Textures.Animated;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Base_Objects.Textures;

public class AssetTexture : SingleTexture
{
    private readonly SingleTexture _texture;
    public SingleTexture Texture => _texture;

    public AssetTexture(string path)
    {
        var asset = AssetManager.GetAsset(path);
        using var source = asset.Stream;

        var image = Image.Load<Rgba32>(source);
        Width = image.Width;
        Height = image.Height;

        if (image.Frames.Count > 1)
            _texture = new AnimatedTexture(image);
        else _texture = new StaticTexture(image);
    }

    public bool IsAnimated => _texture is AnimatedTexture;

    public override bool NeedsUploading()
    {
        return _texture.NeedsUploading();
    }

    public override void Update()
    {
        _texture.Update();
    }

    public override void UploadToGPU(bool dispose)
    {
        _texture.UploadToGPU(dispose);
    }

    public override void Bind(TextureUnit slot = TextureUnit.Texture0)
    {
        _texture.Bind(slot);
    }

    public override void Dispose()
    {
        _texture.Dispose();
        GC.SuppressFinalize(this);
    }
}
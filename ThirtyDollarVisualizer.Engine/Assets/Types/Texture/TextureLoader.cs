using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ThirtyDollarVisualizer.Engine.Assets.Abstract;
using ThirtyDollarVisualizer.Engine.Assets.Types.Asset;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Texture;

public class TextureLoader : IAssetLoader<TextureHolder, TextureInfo>
{
    public bool Query(TextureInfo createInfo, AssetProvider assetProvider)
    {
        return assetProvider.Query<AssetStream, AssetInfo>(createInfo.AssetInfo);
    }
    
    public TextureHolder Load(TextureInfo createInfo, AssetProvider assetProvider,
        Func<TextureInfo, AssetProvider, TextureHolder> create)
    {
        return create(createInfo, assetProvider);
    }

    public TextureHolder Load(TextureInfo createInfo, AssetProvider assetProvider)
    {
        return Load(createInfo, assetProvider, Create);
    }

    public static TextureHolder Create(TextureInfo createInfo, AssetProvider assetProvider)
    {
        var assetInfo = createInfo.AssetInfo;
        var assetStream = assetProvider.Load<AssetStream, AssetInfo>(assetInfo);
        
        using var stream = assetStream.Stream;
        var image = Image.Load<Rgba32>(stream);

        switch (image.Width, image.Height)
        {
            case (not 0, not 0):
            {
                image.Mutate(context => context.Resize(new ResizeOptions()
                {
                    Mode = ResizeMode.Stretch,
                    Size = new Size(createInfo.Width, createInfo.Height)
                }));
                break;
            }

            case (not 0, 0):
            {
                var scale = createInfo.Width / (float)image.Width;
                image.Mutate(context => context.Resize(new ResizeOptions
                    { Mode = ResizeMode.Max, Size = new Size(createInfo.Width, (int)(image.Height * scale)) }));
                break;
            }

            case (0, not 0):
            {
                var scale = createInfo.Height / (float)image.Height;
                image.Mutate(context => context.Resize(new ResizeOptions
                    { Mode = ResizeMode.Max, Size = new Size((int)(image.Width * scale), createInfo.Height) }));
                break;
            }
        }

        createInfo.Width = image.Width;
        createInfo.Height = image.Height;
        createInfo.IsAnimated = image.Frames.Count > 1;

        return new TextureHolder
        {
            TextureInfo = createInfo,
            Texture = image
        };
    }
}
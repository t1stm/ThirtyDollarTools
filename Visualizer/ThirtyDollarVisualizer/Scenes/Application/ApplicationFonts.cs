using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Text;
using ThirtyDollarVisualizer.Engine.Text.Fonts;

namespace ThirtyDollarVisualizer.Scenes.Application;

public class ApplicationFonts
{
    public ApplicationFonts(AssetProvider assetProvider)
    {
        var fontProvider = new FontProvider(assetProvider);
        LatoRegularProvider = new TextProvider(assetProvider, fontProvider,
            "Lato Regular");
        LatoBoldProvider = new TextProvider(assetProvider, fontProvider, "Lato Bold");
        TwemojiProvider = new TextProvider(assetProvider, fontProvider, "Twemoji Mozilla");
    }

    public TextProvider LatoRegularProvider { get; }
    public TextProvider LatoBoldProvider { get; }
    public TextProvider TwemojiProvider { get; }
}
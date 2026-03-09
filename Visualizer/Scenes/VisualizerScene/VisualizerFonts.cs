using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Fonts;

namespace VisualizerScene;

public class VisualizerFonts
{
    public VisualizerFonts(AssetProvider assetProvider)
    {
        var fontProvider = new FontProvider(assetProvider);
        LatoRegularProvider = new TextProvider(assetProvider, fontProvider,
            "Lato Regular");
        LatoBoldProvider = new TextProvider(assetProvider, fontProvider, "Lato Bold");
        TwemojiProvider = new TextProvider(assetProvider, fontProvider, "Twemoji Mozilla");
        DeleteQueue = assetProvider.DeleteQueue;
    }

    public TextProvider LatoRegularProvider { get; }
    public TextProvider LatoBoldProvider { get; }
    public TextProvider TwemojiProvider { get; }
    public DeleteQueue DeleteQueue { get; }
}
using Sundex.Engine.AssetManagement;
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
    }

    public TextProvider LatoRegularProvider { get; }
    public TextProvider LatoBoldProvider { get; }
}

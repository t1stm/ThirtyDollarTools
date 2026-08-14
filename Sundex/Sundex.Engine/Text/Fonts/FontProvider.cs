using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.MSDF;

namespace Sundex.Engine.Text.Fonts;

public class FontProvider : IFontProvider
{
    public FontProvider(AssetProvider assetProvider)
    {
        AddFont("Lato Regular", assetProvider.Load<AssetStream, AssetInfo>(new AssetInfo
        {
            Location = "Assets/Fonts/Lato-Regular.ttf"
        }));

        AddFont("Lato Bold", assetProvider.Load<AssetStream, AssetInfo>(new AssetInfo
        {
            Location = "Assets/Fonts/Lato-Bold.ttf"
        }));

        AddFont("Twemoji Mozilla", assetProvider.Load<AssetStream, AssetInfo>(new AssetInfo
        {
            Location = "Assets/Fonts/Twemoji.Mozilla.ttf"
        }));
    }

    /// <summary>Every bundled font, parsed at construction and kept for the process lifetime.</summary>
    private Dictionary<string, MsdfFont> LoadedFonts { get; } = new();

    public MsdfFont GetFont(ReadOnlySpan<char> fontName)
    {
        var lookup = LoadedFonts.GetAlternateLookup<ReadOnlySpan<char>>();

        return lookup.TryGetValue(fontName, out var font)
            ? font
            : throw new Exception($"Unable to find font: {fontName}");
    }

    private void AddFont(string fontName, AssetStream assetStream)
    {
        var lookup = LoadedFonts.GetAlternateLookup<ReadOnlySpan<char>>();
        lookup.TryAdd(fontName, MsdfFont.Load(assetStream.Stream));
    }
}

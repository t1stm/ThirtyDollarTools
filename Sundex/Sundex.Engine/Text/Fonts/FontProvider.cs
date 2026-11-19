using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Slug;

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

        AddFont("Lucide", assetProvider.Load<AssetStream, AssetInfo>(new AssetInfo
        {
            Location = "Assets/Fonts/lucide.ttf"
        }));
    }

    /// <summary>Every bundled font, parsed at construction and kept for the process lifetime.</summary>
    private Dictionary<string, SlugFont> LoadedFonts { get; } = new();

    public SlugFont GetFont(ReadOnlySpan<char> fontName)
    {
        var lookup = LoadedFonts.GetAlternateLookup<ReadOnlySpan<char>>();

        return lookup.TryGetValue(fontName, out var font)
            ? font
            : throw new Exception($"Unable to find font: {fontName}");
    }

    /// <summary>
    ///     Adds a font the app bundles, by the name labels ask for it by (<c>font = "Golos-500"</c>).
    ///     The glyph data is built on a worker straight away, so the first label in it doesn't wait.
    /// </summary>
    public void AddFont(string fontName, Stream stream)
    {
        var font = SlugFont.Load(stream);
        LoadedFonts[fontName] = font;
        Task.Run(() => _ = font.SlugData);
    }

    /// <summary>Makes <paramref name="alias" /> another name for a loaded font: how an app replaces the default.</summary>
    public void Alias(string alias, string fontName) => LoadedFonts[alias] = LoadedFonts[fontName];

    private void AddFont(string fontName, AssetStream assetStream)
    {
        var lookup = LoadedFonts.GetAlternateLookup<ReadOnlySpan<char>>();
        lookup.TryAdd(fontName, SlugFont.Load(assetStream.Stream));
    }
}

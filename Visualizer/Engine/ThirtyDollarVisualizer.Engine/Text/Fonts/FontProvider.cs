using Msdfgen.Extensions;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Asset;

namespace ThirtyDollarVisualizer.Engine.Text.Fonts;

public class FontProvider
{
    private readonly FreetypeHandle _freetypeHandle;

    public FontProvider(AssetProvider assetProvider)
    {
        _freetypeHandle = FreetypeHandle.Initialize()
                          ?? throw new Exception("Unable to initialize FreeType library.");

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

    private Dictionary<string, byte[]> LoadedFontBytes { get; } = new();

    private void AddFont(string fontName, AssetStream assetStream)
    {
        var length = (int)assetStream.Stream.Length;
        var array = new byte[length];
        assetStream.Stream.ReadExactly(array);

        var lookup = LoadedFontBytes.GetAlternateLookup<ReadOnlySpan<char>>();
        lookup.TryAdd(fontName, array);
    }

    public FontHandle GetFont(ReadOnlySpan<char> fontName)
    {
        var lookup = LoadedFontBytes.GetAlternateLookup<ReadOnlySpan<char>>();
        
        return lookup.TryGetValue(fontName, out var font)
            ? FontHandle.LoadFontData(_freetypeHandle, font) ?? throw new Exception("Unable to load font data.")
            : throw new Exception($"Unable to find font bytes for: {fontName}");
    }
}
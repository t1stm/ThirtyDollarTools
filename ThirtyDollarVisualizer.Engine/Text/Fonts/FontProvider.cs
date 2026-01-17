using Msdfgen.Extensions;
using ThirtyDollarVisualizer.Engine.Assets;
using ThirtyDollarVisualizer.Engine.Assets.Types.Asset;

namespace ThirtyDollarVisualizer.Engine.Text.Fonts;

public class FontProvider
{
    private Dictionary<string, FontHandle> LoadedFonts { get; } = new();
    private readonly FreetypeHandle _freetypeHandle;
    
    public FontProvider(AssetProvider assetProvider)
    {
        _freetypeHandle = FreetypeHandle.Initialize() 
                          ?? throw new Exception("Unable to initialize FreeType library.");
        
        AddFont("Lato Regular", assetProvider.Load<AssetStream, AssetInfo>(new AssetInfo
        {
            Location = "Assets/Fonts/Lato-Regular.ttf",
        }));
        
        AddFont("Lato Bold", assetProvider.Load<AssetStream, AssetInfo>(new AssetInfo
        {
            Location = "Assets/Fonts/Lato-Bold.ttf",
        }));
        
        AddFont("Twemoji Mozilla", assetProvider.Load<AssetStream, AssetInfo>(new AssetInfo
        {
            Location = "Assets/Fonts/Twemoji.Mozilla.ttf",
        }));
    }

    private void AddFont(string fontName, AssetStream assetStream)
    {
        var length = (int)assetStream.Stream.Length;
        var array = new byte[length];
        
        assetStream.Stream.ReadExactly(array);
        
        var font = FontHandle.LoadFontData(_freetypeHandle, array);
        if (font == null) throw new Exception("Unable to load font.");

        var lookup = LoadedFonts.GetAlternateLookup<ReadOnlySpan<char>>();
        lookup.TryAdd(fontName, font);
    }

    public FontHandle GetFont(ReadOnlySpan<char> fontName)
    {
        var lookup = LoadedFonts.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(fontName, out var font) ? font : throw new Exception($"Unable to find font: {fontName}");
    }
}
using System.Reflection;
using SixLabors.Fonts;
using ThirtyDollarVisualizer.Assets;

namespace ThirtyDollarVisualizer.Base_Objects.Text;

public static class Fonts
{
    private static FontCollection? _collection;
    private static FontFamily? _currentFamily;
    private static FontFamily? _emojiFamily;
    private static CharacterCache? _characterCache;

    public static void Initialize()
    {
        _collection = new FontCollection();
        _collection.AddSystemFonts();
        
        AddFont(_collection, Asset.Embedded("Fonts/Lato-Regular.ttf"));
        AddFont(_collection, Asset.Embedded("Fonts/Lato-Bold.ttf"));
        AddFont(_collection, Asset.Embedded("Fonts/Twemoji.Mozilla.ttf"));

        const string textFont = "Lato";
        const string emojiFont = "Twemoji Mozilla";
        if (!_collection.TryGet(textFont, out var text_family))
            throw new Exception($"Unable to find font: {textFont}");
        if (!_collection.TryGet(emojiFont, out var emoji_family))
            throw new Exception($"Unable to find font: {emojiFont}");

        _currentFamily = text_family;
        _emojiFamily = emoji_family;
        _characterCache = new CharacterCache(text_family, emoji_family);
    }

    private static void AddFont(FontCollection collection, string location)
    {
        using var stream = AssetManager.GetAsset(location).Stream;

        if (stream == null)
            throw new NullReferenceException($"Unable to load the font \'{location}\'.");
        collection.Add(stream);
    }

    public static FontFamily GetFontFamily()
    {
        if (_currentFamily == null) Initialize();
        return (FontFamily)_currentFamily!;
    }

    public static FontFamily GetEmojiFamily()
    {
        if (_currentFamily == null) Initialize();
        return (FontFamily)_emojiFamily!;
    }

    public static CharacterCache GetCharacterCache()
    {
        if (_currentFamily == null) Initialize();
        return _characterCache!;
    }
}
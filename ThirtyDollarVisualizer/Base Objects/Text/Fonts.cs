using System.Reflection;
using SixLabors.Fonts;

namespace ThirtyDollarVisualizer.Objects.Text;

public static class Fonts
{
    private static FontCollection? Collection;
    private static FontFamily? CurrentFamily;
    private static FontFamily? EmojiFamily;
    private static CharacterCache? CharacterCache;
    
    public static void Initialize()
    {
        Collection = new FontCollection();
        Collection.AddSystemFonts();

        AddFont(Collection, "ThirtyDollarVisualizer.Assets.Fonts.Lato-Regular.ttf");
        AddFont(Collection, "ThirtyDollarVisualizer.Assets.Fonts.Lato-Bold.ttf");
        AddFont(Collection, "ThirtyDollarVisualizer.Assets.Fonts.Twemoji.Mozilla.ttf");
        
        const string text_font = "Lato";
        const string emoji_font = "Twemoji Mozilla";
        if (!Collection.TryGet(text_font, out var text_family)) throw new Exception($"Unable to find font: {text_font}");
        if (!Collection.TryGet(emoji_font, out var emoji_family)) throw new Exception($"Unable to find font: {emoji_font}"); 
        
        CurrentFamily = text_family;
        EmojiFamily = emoji_family;
        CharacterCache = new CharacterCache(text_family, emoji_family);
    }

    private static void AddFont(FontCollection collection, string location)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(location);

        if (stream == null) throw new NullReferenceException($"This project was compiled without the \'{location}\' font.");
        collection.Add(stream);
    }

    public static FontFamily GetFontFamily()
    {
        if (CurrentFamily == null) Initialize();
        return (FontFamily) CurrentFamily!;
    }
    
    public static FontFamily GetEmojiFamily()
    {
        if (CurrentFamily == null) Initialize();
        return (FontFamily) EmojiFamily!;
    }

    public static CharacterCache GetCharacterCache()
    {
        if (CurrentFamily == null) Initialize();
        return CharacterCache!;
    }
}
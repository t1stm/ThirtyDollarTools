using System.Reflection;
using SixLabors.Fonts;

namespace ThirtyDollarVisualizer.Objects.Text;

public static class Fonts
{
    private static FontCollection? Collection;
    private static FontFamily? CurrentFamily;
    private static CharacterCache? CharacterCache;
    
    public static void Initialize()
    {
        Collection = new FontCollection();
        Collection.AddSystemFonts();

        AddFont(Collection, "ThirtyDollarVisualizer.Assets.Fonts.Lato-Regular.ttf");
        AddFont(Collection, "ThirtyDollarVisualizer.Assets.Fonts.Lato-Bold.ttf");
        
        const string font_name = "Lato";
        if (!Collection.TryGet(font_name, out var family)) throw new Exception($"Unable to find font: {font_name}"); 
        
        CurrentFamily = family;
        CharacterCache = new CharacterCache(family);
    }

    private static void AddFont(FontCollection collection, string location)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(location);

        if (stream == null) throw new NullReferenceException("This project was compiled without the \'Lato-Regular\' font.");
        collection.Add(stream);
    }

    public static FontFamily GetFontFamily()
    {
        if (CurrentFamily == null) Initialize();
        return (FontFamily) CurrentFamily!;
    }

    public static CharacterCache GetCharacterCache()
    {
        if (CurrentFamily == null) Initialize();
        return CharacterCache!;
    }
}
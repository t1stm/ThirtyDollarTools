using System.Reflection;
using SixLabors.Fonts;

namespace ThirtyDollarVisualizer.Objects.Text;

public static class Fonts
{
    private static FontCollection? Collection;
    private static FontFamily? CurrentFamily;
    public static void Initialize()
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("ThirtyDollarVisualizer.Assets.Fonts.Lato-Regular.ttf");

        if (stream == null) throw new NullReferenceException("This project was compiled without the \'Lato-Regular\' font.");

        var collection = new FontCollection();
        collection.AddSystemFonts();
        collection.Add(stream);
        Collection = collection;

        const string font_name = "Lato";
        if (!collection.TryGet(font_name, out var family)) throw new Exception($"Unable to find font: {font_name}"); 
        
        CurrentFamily = family;
    }

    public static FontFamily GetFontFamily()
    {
        if (CurrentFamily == null) Initialize();
        return (FontFamily) CurrentFamily!;
    }
}
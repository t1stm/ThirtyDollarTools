namespace Sundex.MSDF.Tests;

/// <summary>The fonts the engine ships, copied into the test output and opened by name.</summary>
internal static class TestFonts
{
    public const string LatoRegular = "Lato-Regular";
    public const string LatoBold = "Lato-Bold";
    public const string Twemoji = "Twemoji.Mozilla";

    public static Stream Open(string font) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fonts", font + ".ttf"));
}

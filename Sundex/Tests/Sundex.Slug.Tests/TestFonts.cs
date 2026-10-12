using System.Collections.Concurrent;

namespace Sundex.Slug.Tests;

/// <summary>The fonts the engine ships, copied into the test output and opened by name.</summary>
internal static class TestFonts
{
    public const string LatoRegular = "Lato-Regular";
    public const string LatoBold = "Lato-Bold";
    public const string Twemoji = "Twemoji.Mozilla";
    public const string Lucide = "lucide";

    private static readonly ConcurrentDictionary<string, Lazy<SlugFont>> SharedFonts = new();

    public static Stream Open(string font) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fonts", font + ".ttf"));

    /// <summary>
    ///     One loaded instance per font for the whole run, so its Slug data is built once and not
    ///     per test. Only for tests that never dispose the font.
    /// </summary>
    public static SlugFont Shared(string font) =>
        SharedFonts.GetOrAdd(font, name => new Lazy<SlugFont>(() =>
        {
            using var stream = Open(name);
            return SlugFont.Load(stream);
        })).Value;
}

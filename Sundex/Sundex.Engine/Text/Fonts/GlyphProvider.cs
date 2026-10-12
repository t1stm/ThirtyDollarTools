using Sundex.Slug;
using Sundex.Slug.Fonts;

namespace Sundex.Engine.Text.Fonts;

public class GlyphProvider(IFontProvider fontProvider, string fontName) : IGlyphProvider
{
    private SlugFontMetrics? _cachedMetrics;

    /// <summary>
    ///     The font's curve and band data. Built on first use, once per font, and shared by
    ///     every provider over the same font.
    /// </summary>
    public SlugFontData SlugData => fontProvider.GetFont(fontName).SlugData;

    public SlugGlyph GetSlugGlyph(ReadOnlySpan<char> character) => SlugData.GetGlyph(character);

    public SlugFontMetrics GetFontMetrics() =>
        _cachedMetrics ??= fontProvider.GetFont(fontName).Metrics;
}

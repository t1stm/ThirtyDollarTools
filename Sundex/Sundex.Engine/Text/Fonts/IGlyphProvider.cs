using Sundex.Slug;
using Sundex.Slug.Fonts;

namespace Sundex.Engine.Text.Fonts;

public interface IGlyphProvider
{
    /// <summary>
    ///     The Slug data for one character, which may be a surrogate pair. A character the font
    ///     lacks gets the font's <c>.notdef</c> glyph.
    /// </summary>
    SlugGlyph GetSlugGlyph(ReadOnlySpan<char> character);

    /// <summary>Returns the metrics for the current font.</summary>
    SlugFontMetrics GetFontMetrics();
}

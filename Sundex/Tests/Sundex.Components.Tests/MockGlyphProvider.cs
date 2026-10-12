using System.Numerics;
using Sundex.Engine.Text.Fonts;
using Sundex.Slug;
using Sundex.Slug.Fonts;

namespace Sundex.Components.Tests;

public class MockGlyphProvider : IGlyphProvider
{
    private const float P = SlugFontData.QuadPadding;

    /// <summary>
    ///     What every character comes back as. The 0.6 em advance is load-bearing:
    ///     TextInputTests depends on 0.6 × 16 px = 9.6 px per character.
    /// </summary>
    public SlugGlyph Glyph { get; set; } = new(0.6f,
        new Vector4(0.05f - P, -0.1f - P, 0.55f + P, 0.7f + P),
        new Vector4(2, 3, -0.2f, 0.4f),
        new Vector4(7, 1, 1, 2));

    public SlugGlyph GetSlugGlyph(ReadOnlySpan<char> character) => Glyph;

    // Em-normalised rather than raw font units, so the numbers stay readable: TextInputTests
    // depends on LineHeight/EmSize being 1.2.
    public SlugFontMetrics GetFontMetrics()
    {
        return new SlugFontMetrics(
            1,
            1,
            -0.2,
            1.2,
            -0.1,
            0.05);
    }
}

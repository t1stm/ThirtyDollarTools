using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.MSDF.Fonts;

namespace Sundex.Engine.Text.Fonts;

public interface IGlyphProvider
{
    /// <summary>Generates and returns an MSDF glyph image for the given character.</summary>
    Image<Rgb48> GetGlyph(ReadOnlySpan<char> character);

    /// <summary>
    ///     Generates a glyph ahead of the frame that first draws it and parks it for the
    ///     next <see cref="GetGlyph" />. Generation is the expensive half of putting a
    ///     character on screen and needs no graphics context, so it can be done from any
    ///     thread; the upload that follows still cannot. Doing nothing is a valid
    ///     implementation - it only costs the caller the generation it was avoiding.
    /// </summary>
    void Warm(ReadOnlySpan<char> character)
    {
    }

    /// <summary>Returns the metrics for the current font.</summary>
    MsdfFontMetrics GetFontMetrics();

    /// <summary>Returns the pre-computed sizing/alignment data for a character that has already been rasterised.</summary>
    TextAlignmentData GetSizingData(ReadOnlySpan<char> character);
}

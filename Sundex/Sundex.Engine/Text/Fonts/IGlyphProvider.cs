using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.MSDF.Fonts;

namespace Sundex.Engine.Text.Fonts;

public interface IGlyphProvider
{
    /// <summary>Generates and returns an MSDF glyph image for the given character.</summary>
    Image<RgbaVector> GetGlyph(ReadOnlySpan<char> character);

    /// <summary>Returns the metrics for the current font.</summary>
    MsdfFontMetrics GetFontMetrics();

    /// <summary>Returns the pre-computed sizing/alignment data for a character that has already been rasterised.</summary>
    TextAlignmentData GetSizingData(ReadOnlySpan<char> character);
}

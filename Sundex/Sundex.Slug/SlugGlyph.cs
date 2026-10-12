using System.Numerics;

namespace Sundex.Slug;

/// <summary>
///     Everything the Slug shader and the layout need for one glyph. All of it is in <b>em space</b>
///     (one unit per em, Y-up, origin on the baseline), and all of it is constant over the glyph's quad.
/// </summary>
/// <param name="Advance">How far the pen moves after drawing the glyph, in ems.</param>
/// <param name="EmRect">
///     The quad's corners in em space: <c>(left, bottom, right, top)</c>, the ink bounds grown by
///     <see cref="SlugFontData.QuadPadding" /> on every side. These are the sample coordinates the
///     shader interpolates across the quad.
/// </param>
/// <param name="Banding">
///     The shader's <c>bandTransform</c>: <c>(scale x, scale y, offset x, offset y)</c>, which maps an
///     em coordinate to a vertical band index (from x) and a horizontal band index (from y).
/// </param>
/// <param name="GlyphData">
///     The shader's <c>glyphData</c>: where the glyph's band headers start in the band texture, and
///     the highest vertical and horizontal band index, <c>(x, y, nv − 1, nh − 1)</c>.
/// </param>
public readonly record struct SlugGlyph(float Advance, Vector4 EmRect, Vector4 Banding, Vector4 GlyphData)
{
    /// <summary>
    ///     The shift that puts the padded ink box's bottom-left corner at the pen position, in ems.
    ///     With <see cref="TextScale" /> this is the <c>Translate</c>/<c>Scale</c> pair the engine's
    ///     line layout already places quads with, at a relative size of 1.
    /// </summary>
    public Vector2 TextTranslate => new(-EmRect.X, -EmRect.Y);

    /// <summary>The reciprocal of the padded ink box's size in ems. See <see cref="TextTranslate" />.</summary>
    public Vector2 TextScale => new(1f / (EmRect.Z - EmRect.X), 1f / (EmRect.W - EmRect.Y));
}

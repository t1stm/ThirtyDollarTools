using System.Numerics;

namespace Sundex.MSDF;

/// <summary>
///     Where one generated glyph sits, in <b>em space</b> (one unit = one em, Y-up, origin at the
///     baseline). The caller needs all three to place the bitmap: the field fills its square, so
///     only the transform used to frame it says where the ink actually belongs.
/// </summary>
/// <param name="Advance">How far the pen moves after drawing the glyph, in ems.</param>
/// <param name="Translate">Shift applied to the outline before rasterising, in ems.</param>
/// <param name="Scale">Scale applied to the outline before rasterising, in output pixels per em.</param>
public readonly record struct MsdfGlyph(double Advance, Vector2 Translate, Vector2 Scale);
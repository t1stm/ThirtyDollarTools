using System.Numerics;
using SixLabors.Fonts;
using SixLabors.Fonts.Rendering;
using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Fonts;

/// <summary>
///     Collects SixLabors.Fonts' outline callbacks into a <see cref="Shape" />.
///     <para>
///         SixLabors emits Y-<i>down</i> coordinates inside its layout box; a shape is
///         <b>em-normalised and Y-up</b>, one unit to the em. The caller renders at
///         <c>Font.Size == UnitsPerEm</c> with <c>Dpi == 72</c>, which makes one incoming unit one
///         font unit, so the conversion is a flip and a divide.
///     </para>
///     <para>
///         That gets horizontal placement right but not vertical: SixLabors grows its line box
///         for ink rising above the ascender, which moves the baseline down for tall glyphs.
///         <see cref="MsdfFont.TryBuildShape" /> puts the finished shape on the baseline itself.
///     </para>
/// </summary>
internal sealed class OutlineGlyphRenderer(Shape shape, double unitsPerEm) : IGlyphRenderer
{
    private readonly double _unitsPerEm = unitsPerEm;

    private Vector2d _current;
    private bool _inFigure;

    public void BeginText(in FontRectangle bounds)
    {
    }

    public void EndText()
    {
    }

    public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
    {
        return true;
    }

    public void EndGlyph()
    {
    }

    public void BeginLayer(Paint paint, FillRule fillRule, ClipQuad? clipBounds)
    {
    }

    public void EndLayer()
    {
    }

    public void BeginFigure()
    {
        shape.BeginContour();
        _inFigure = true;
    }

    public void EndFigure()
    {
        if (!_inFigure) return;
        shape.EndContour();
        _inFigure = false;
    }

    public void MoveTo(Vector2 point)
    {
        _current = Transform(point);
    }

    public void LineTo(Vector2 point)
    {
        var next = Transform(point);
        Emit(EdgeSegment.Linear(_current, next));
        _current = next;
    }

    public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point)
    {
        var c = Transform(secondControlPoint);
        var next = Transform(point);
        Emit(EdgeSegment.Quadratic(_current, c, next));
        _current = next;
    }

    public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)
    {
        var c1 = Transform(secondControlPoint);
        var c2 = Transform(thirdControlPoint);
        var next = Transform(point);
        Emit(EdgeSegment.Cubic(_current, c1, c2, next));
        _current = next;
    }

    // ponytail: arcs only reach a glyph renderer through SVG-in-OpenType, which MsdfFont never
    // asks for, so the arc is chorded to a straight line rather than approximated. An unexpected
    // font degrades instead of crashing text rendering. Split it into cubics if an SVG-table font
    // ever has to work.
    public void ArcTo(float radiusX, float radiusY, float rotation, bool largeArc, bool sweep, Vector2 point)
    {
        LineTo(point);
    }

    public TextDecorations EnabledDecorations()
    {
        return TextDecorations.None;
    }

    public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness)
    {
    }

    private void Emit(EdgeSegment edge)
    {
        // A zero-length edge has no direction, which edge colouring's corner detection needs.
        if (edge.P0 == edge.EndPoint) return;
        shape.AddEdge(edge);
    }

    private Vector2d Transform(Vector2 p)
    {
        return new Vector2d(p.X / _unitsPerEm, -p.Y / _unitsPerEm);
    }
}
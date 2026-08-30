using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Distance;

/// <summary>
///     What one edge worked out to at the previously sampled point: where that point was, and how
///     far the edge was from it. The next pixel is close by, so these bounds let it skip edges
///     that cannot possibly be its nearest without evaluating them.
///     <para>
///         One array of these lives on the <see cref="ShapeDistanceFinder" /> and is reused for
///         every pixel of every glyph.
///     </para>
///     <para>
///         <see cref="TrueDistanceSelector" /> only uses the first two fields; sharing one cache
///         type keeps the finder to a single scratch buffer.
///     </para>
/// </summary>
internal struct EdgeCache
{
    public Vector2d Point;
    public double AbsDistance;
    public double ADomainDistance;
    public double BDomainDistance;
    public double APerpendicularDistance;
    public double BPerpendicularDistance;
}

/// <summary>One signed distance per output channel.</summary>
internal struct MultiDistance
{
    public double R;
    public double G;
    public double B;
}
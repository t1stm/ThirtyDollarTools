using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Distance;

/// <summary>
///     Tracks the plain nearest-edge signed distance to a point: no perpendicular extension, no
///     per-channel split. Edges are fed in one at a time and the nearest wins.
///     <para>Used for the winding probe in <c>MsdfFont</c>, not for the MSDF itself.</para>
/// </summary>
internal struct TrueDistanceSelector
{
    /// <summary>
    ///     How much a cached distance bound is loosened per unit the sample point moves. Slightly
    ///     over 1 so rounding can never make the bound reject an edge it should have kept.
    /// </summary>
    public const double DistanceDeltaFactor = 1.001;

    private Vector2d _p;
    private SignedDistance _minDistance;

    public static TrueDistanceSelector Create() => new() { _minDistance = SignedDistance.Initial };

    /// <summary>
    ///     Moves the selector to a new sample point. The distance found at the last point is
    ///     carried over, loosened by how far the point moved, so it starts from a usable bound
    ///     instead of from infinity.
    /// </summary>
    public void Reset(Vector2d p)
    {
        var delta = DistanceDeltaFactor * (p - _p).Length;
        _minDistance.Distance += NonZeroSign(_minDistance.Distance) * delta;
        _p = p;
    }

    /// <summary>Offers one edge to the search, skipping it when the cache proves it cannot win.</summary>
    public void AddEdge(ref EdgeCache cache, in EdgeSegment edge)
    {
        var delta = DistanceDeltaFactor * (_p - cache.Point).Length;
        if (cache.AbsDistance - delta > Math.Abs(_minDistance.Distance)) return;

        var distance = edge.SignedDistance(_p, out _);
        if (distance < _minDistance) _minDistance = distance;

        cache.Point = _p;
        cache.AbsDistance = Math.Abs(distance.Distance);
    }

    public readonly double Distance() => _minDistance.Distance;

    private static double NonZeroSign(double n) => n > 0 ? 1 : -1;
}

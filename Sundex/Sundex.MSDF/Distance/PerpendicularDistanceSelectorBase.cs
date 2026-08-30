using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Distance;

/// <summary>
///     The distance search for one output channel. Tracks both the nearest true distance and the
///     nearest <i>perpendicular</i> distance — the distance to an edge's infinite extension —
///     keeping the closest of the latter on each side of the outline separately.
///     <para>
///         The perpendicular term is what lets the field stay linear across a corner instead of
///         curving around the vertex, and that is what keeps corners sharp once the shader takes
///         the median of the three channels.
///     </para>
/// </summary>
internal struct PerpendicularDistanceSelectorBase
{
    private SignedDistance _minTrueDistance;
    private double _minNegativePerpendicularDistance;
    private double _minPositivePerpendicularDistance;

    /// <summary>The nearest edge so far, as an index into the shape's edge array; -1 for none yet.</summary>
    private int _nearEdge;

    private double _nearEdgeParam;

    public static PerpendicularDistanceSelectorBase Create()
    {
        var initial = SignedDistance.Initial;
        return new PerpendicularDistanceSelectorBase
        {
            _minTrueDistance = initial,
            _minNegativePerpendicularDistance = -Math.Abs(initial.Distance),
            _minPositivePerpendicularDistance = Math.Abs(initial.Distance),
            _nearEdge = -1,
            _nearEdgeParam = 0
        };
    }

    /// <summary>
    ///     Replaces <paramref name="distance" /> with the distance to the infinite extension of an
    ///     edge, when the point lies beyond the end <paramref name="ep" /> is measured from and
    ///     that extension is nearer. Returns true when it did.
    /// </summary>
    public static bool GetPerpendicularDistance(ref double distance, Vector2d ep, Vector2d edgeDir)
    {
        var ts = Vector2d.Dot(ep, edgeDir);
        if (ts <= 0) return false;

        var perpendicularDistance = Vector2d.Cross(ep, edgeDir);
        if (Math.Abs(perpendicularDistance) >= Math.Abs(distance)) return false;

        distance = perpendicularDistance;
        return true;
    }

    /// <summary>
    ///     Starts the search at a new sample point <paramref name="delta" /> away, keeping the
    ///     previous point's distance as an opening bound rather than restarting from infinity.
    /// </summary>
    public void Reset(double delta)
    {
        _minTrueDistance.Distance += NonZeroSign(_minTrueDistance.Distance) * delta;
        _minNegativePerpendicularDistance = -Math.Abs(_minTrueDistance.Distance);
        _minPositivePerpendicularDistance = Math.Abs(_minTrueDistance.Distance);
        _nearEdge = -1;
        _nearEdgeParam = 0;
    }

    /// <summary>
    ///     Whether an edge could still be the nearest, judged from what it measured at the
    ///     previously sampled point. False means it can be skipped without evaluating it.
    /// </summary>
    public readonly bool IsEdgeRelevant(in EdgeCache cache, Vector2d p)
    {
        var delta = TrueDistanceSelector.DistanceDeltaFactor * (p - cache.Point).Length;

        return cache.AbsDistance - delta <= Math.Abs(_minTrueDistance.Distance)
               || Math.Abs(cache.ADomainDistance) < delta
               || Math.Abs(cache.BDomainDistance) < delta
               || (cache.ADomainDistance > 0 && (cache.APerpendicularDistance < 0
                   ? cache.APerpendicularDistance + delta >= _minNegativePerpendicularDistance
                   : cache.APerpendicularDistance - delta <= _minPositivePerpendicularDistance))
               || (cache.BDomainDistance > 0 && (cache.BPerpendicularDistance < 0
                   ? cache.BPerpendicularDistance + delta >= _minNegativePerpendicularDistance
                   : cache.BPerpendicularDistance - delta <= _minPositivePerpendicularDistance));
    }

    public void AddEdgeTrueDistance(int edgeIndex, SignedDistance distance, double param)
    {
        if (!(distance < _minTrueDistance)) return;

        _minTrueDistance = distance;
        _nearEdge = edgeIndex;
        _nearEdgeParam = param;
    }

    public void AddEdgePerpendicularDistance(double distance)
    {
        if (distance <= 0 && distance > _minNegativePerpendicularDistance)
            _minNegativePerpendicularDistance = distance;
        if (distance >= 0 && distance < _minPositivePerpendicularDistance)
            _minPositivePerpendicularDistance = distance;
    }

    /// <summary>Folds another selector's findings for the same point into this one.</summary>
    public void Merge(in PerpendicularDistanceSelectorBase other)
    {
        if (other._minTrueDistance < _minTrueDistance)
        {
            _minTrueDistance = other._minTrueDistance;
            _nearEdge = other._nearEdge;
            _nearEdgeParam = other._nearEdgeParam;
        }

        if (other._minNegativePerpendicularDistance > _minNegativePerpendicularDistance)
            _minNegativePerpendicularDistance = other._minNegativePerpendicularDistance;
        if (other._minPositivePerpendicularDistance < _minPositivePerpendicularDistance)
            _minPositivePerpendicularDistance = other._minPositivePerpendicularDistance;
    }

    /// <summary>
    ///     The channel's final distance: the nearest perpendicular distance on the side the
    ///     nearest edge put the point, tightened by that edge's own perpendicular distance where
    ///     the point projects past its ends.
    /// </summary>
    public readonly double ComputeDistance(Vector2d p, ReadOnlySpan<EdgeSegment> edges)
    {
        var minDistance = _minTrueDistance.Distance < 0
            ? _minNegativePerpendicularDistance
            : _minPositivePerpendicularDistance;

        if (_nearEdge < 0) return minDistance;

        var distance = _minTrueDistance;
        edges[_nearEdge].DistanceToPerpendicularDistance(ref distance, p, _nearEdgeParam);
        if (Math.Abs(distance.Distance) < Math.Abs(minDistance)) minDistance = distance.Distance;

        return minDistance;
    }

    public readonly SignedDistance TrueDistance()
    {
        return _minTrueDistance;
    }

    private static double NonZeroSign(double n)
    {
        return n > 0 ? 1 : -1;
    }
}
using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Distance;

/// <summary>
///     The distance search for all three channels at once: three
///     <see cref="PerpendicularDistanceSelectorBase" />s, each fed only by the edges whose
///     <see cref="EdgeColor" /> includes its channel. The median of the three is what the shader
///     reconstructs the sharp outline from.
/// </summary>
internal struct MultiDistanceSelector
{
    private Vector2d _p;
    private PerpendicularDistanceSelectorBase _r;
    private PerpendicularDistanceSelectorBase _g;
    private PerpendicularDistanceSelectorBase _b;

    public static MultiDistanceSelector Create() => new()
    {
        _r = PerpendicularDistanceSelectorBase.Create(),
        _g = PerpendicularDistanceSelectorBase.Create(),
        _b = PerpendicularDistanceSelectorBase.Create()
    };

    /// <summary>Moves all three channels to a new sample point, carrying their bounds over.</summary>
    public void Reset(Vector2d p)
    {
        var delta = TrueDistanceSelector.DistanceDeltaFactor * (p - _p).Length;
        _r.Reset(delta);
        _g.Reset(delta);
        _b.Reset(delta);
        _p = p;
    }

    /// <summary>
    ///     Offers one edge to the channels it is coloured for, skipping the work entirely when
    ///     none of them can still be beaten by it. The neighbouring edges are needed to know where
    ///     this edge's angular domain ends and theirs begins.
    /// </summary>
    public void AddEdge(ref EdgeCache cache, in EdgeSegment prevEdge, in EdgeSegment edge, in EdgeSegment nextEdge,
        int edgeIndex)
    {
        var color = edge.Color;
        var red = (color & EdgeColor.Red) != 0;
        var green = (color & EdgeColor.Green) != 0;
        var blue = (color & EdgeColor.Blue) != 0;

        if (!((red && _r.IsEdgeRelevant(cache, _p)) ||
              (green && _g.IsEdgeRelevant(cache, _p)) ||
              (blue && _b.IsEdgeRelevant(cache, _p))))
            return;

        var distance = edge.SignedDistance(_p, out var param);
        if (red) _r.AddEdgeTrueDistance(edgeIndex, distance, param);
        if (green) _g.AddEdgeTrueDistance(edgeIndex, distance, param);
        if (blue) _b.AddEdgeTrueDistance(edgeIndex, distance, param);

        cache.Point = _p;
        cache.AbsDistance = Math.Abs(distance.Distance);

        var ap = _p - edge.P0;
        var bp = _p - edge.EndPoint;
        var aDir = edge.Direction(0).Normalize(true);
        var bDir = edge.Direction(1).Normalize(true);
        var prevDir = prevEdge.Direction(1).Normalize(true);
        var nextDir = nextEdge.Direction(0).Normalize(true);

        // How far into this edge's angular domain the point sits, measured against the
        // corner bisectors it shares with its neighbours.
        var add = Vector2d.Dot(ap, (prevDir + aDir).Normalize(true));
        var bdd = -Vector2d.Dot(bp, (bDir + nextDir).Normalize(true));

        if (add > 0)
        {
            var pd = distance.Distance;
            if (PerpendicularDistanceSelectorBase.GetPerpendicularDistance(ref pd, ap, -aDir))
            {
                pd = -pd;
                if (red) _r.AddEdgePerpendicularDistance(pd);
                if (green) _g.AddEdgePerpendicularDistance(pd);
                if (blue) _b.AddEdgePerpendicularDistance(pd);
            }

            cache.APerpendicularDistance = pd;
        }

        if (bdd > 0)
        {
            var pd = distance.Distance;
            if (PerpendicularDistanceSelectorBase.GetPerpendicularDistance(ref pd, bp, bDir))
            {
                if (red) _r.AddEdgePerpendicularDistance(pd);
                if (green) _g.AddEdgePerpendicularDistance(pd);
                if (blue) _b.AddEdgePerpendicularDistance(pd);
            }

            cache.BPerpendicularDistance = pd;
        }

        cache.ADomainDistance = add;
        cache.BDomainDistance = bdd;
    }

    /// <summary>Folds another selector's findings for the same point into this one, channel by channel.</summary>
    public void Merge(in MultiDistanceSelector other)
    {
        _r.Merge(other._r);
        _g.Merge(other._g);
        _b.Merge(other._b);
    }

    /// <summary>The three channels' distances at the current sample point.</summary>
    public readonly MultiDistance Distance(ReadOnlySpan<EdgeSegment> edges) => new()
    {
        R = _r.ComputeDistance(_p, edges),
        G = _g.ComputeDistance(_p, edges),
        B = _b.ComputeDistance(_p, edges)
    };

    /// <summary>The nearest true distance any of the three channels found.</summary>
    public readonly SignedDistance TrueDistance()
    {
        var distance = _r.TrueDistance();
        if (_g.TrueDistance() < distance) distance = _g.TrueDistance();
        if (_b.TrueDistance() < distance) distance = _b.TrueDistance();
        return distance;
    }
}

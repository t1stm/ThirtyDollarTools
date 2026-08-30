using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Distance;

/// <summary>
///     Measures the signed distance from a point to a whole shape, by walking every edge of every
///     contour into one selector and asking it for the answer.
///     <para>
///         Both the edge cache and the selectors are members, so one finder is reused for every
///         pixel of a glyph: each pixel's search opens with the previous pixel's bound, and it is
///         that bound which lets most edges be rejected untouched.
///     </para>
///     <para>
///         ponytail: one selector takes all contours together, which is only exact where a
///         glyph's contours do not overlap. Give each contour its own selector and combine them
///         with <c>Merge</c> (both selectors already implement it) if a font ever renders with
///         corrupted overlaps.
///     </para>
/// </summary>
internal sealed class ShapeDistanceFinder
{
    private EdgeCache[] _cache = new EdgeCache[64];

    private MultiDistanceSelector _multi = MultiDistanceSelector.Create();

    private TrueDistanceSelector _true = TrueDistanceSelector.Create();

    /// <summary>The three per-channel distances at <paramref name="origin" /> — the MSDF itself.</summary>
    public MultiDistance MultiDistance(Shape shape, Vector2d origin)
    {
        EnsureCache(shape.EdgeCount);
        _multi.Reset(origin);

        var edges = shape.Edges;
        foreach (var contour in shape.Contours)
        {
            var n = contour.Count;
            if (n == 0) continue;

            for (var i = 0; i < n; i++)
            {
                var cur = contour.Start + (i - 1 + n) % n;
                var prev = contour.Start + (i - 2 + 2 * n) % n;
                var next = contour.Start + i;

                _multi.AddEdge(ref _cache[contour.Start + i], edges[prev], edges[cur], edges[next], cur);
            }
        }

        return _multi.Distance(edges);
    }

    /// <summary>
    ///     The plain signed distance at <paramref name="origin" />, ignoring edge colours. Used
    ///     only by the winding probe.
    /// </summary>
    public double TrueDistance(Shape shape, Vector2d origin)
    {
        EnsureCache(shape.EdgeCount);
        _true.Reset(origin);

        var edges = shape.Edges;
        foreach (var contour in shape.Contours)
        {
            var n = contour.Count;
            if (n == 0) continue;

            for (var i = 0; i < n; i++)
            {
                var cur = contour.Start + (i - 1 + n) % n;
                _true.AddEdge(ref _cache[contour.Start + i], edges[cur]);
            }
        }

        return _true.Distance();
    }

    /// <summary>
    ///     Starts on a new shape. Must be called before the first query: the cache is keyed by
    ///     edge slot, so its entries mean nothing once the edges behind them have changed, and the
    ///     carried distance bounds are likewise stale.
    /// </summary>
    public void Reset(int edgeCount)
    {
        EnsureCache(edgeCount);
        Array.Clear(_cache, 0, edgeCount);
        _multi = MultiDistanceSelector.Create();
        _true = TrueDistanceSelector.Create();
    }

    private void EnsureCache(int edgeCount)
    {
        if (_cache.Length >= edgeCount) return;
        _cache = new EdgeCache[Math.Max(edgeCount, _cache.Length * 2)];
    }
}
namespace Sundex.Slug.Geometry;

internal struct ContourRange
{
    public int Start;
    public int Count;
}

/// <summary>
///     A glyph outline: closed contours of <see cref="EdgeSegment" />s, in em space.
///     <para>
///         Flattened into two growable arrays — every edge of every contour lives in one buffer,
///         and a contour is a (start, count) range into it. A <see cref="Shape" /> is meant to be
///         allocated once and <see cref="Clear" />ed between glyphs.
///     </para>
/// </summary>
internal sealed class Shape
{
    private ContourRange[] _contours = new ContourRange[8];
    private EdgeSegment[] _edges = new EdgeSegment[128];

    public int EdgeCount { get; private set; }
    public int ContourCount { get; private set; }

    public Span<EdgeSegment> Edges => _edges.AsSpan(0, EdgeCount);
    public ReadOnlySpan<ContourRange> Contours => _contours.AsSpan(0, ContourCount);

    public Span<EdgeSegment> ContourEdges(int contour)
    {
        return _edges.AsSpan(_contours[contour].Start, _contours[contour].Count);
    }

    public void Clear()
    {
        EdgeCount = 0;
        ContourCount = 0;
    }

    public void BeginContour()
    {
        if (ContourCount == _contours.Length) Array.Resize(ref _contours, _contours.Length * 2);
        _contours[ContourCount++] = new ContourRange { Start = EdgeCount, Count = 0 };
    }

    public void AddEdge(EdgeSegment edge)
    {
        if (ContourCount == 0) BeginContour();
        if (EdgeCount == _edges.Length) Array.Resize(ref _edges, _edges.Length * 2);
        _edges[EdgeCount++] = edge;
        _contours[ContourCount - 1].Count++;
    }

    /// <summary>Closes the current contour, dropping it if no edge was ever added to it.</summary>
    public void EndContour()
    {
        if (ContourCount > 0 && _contours[ContourCount - 1].Count == 0) ContourCount--;
    }

    /// <summary>True when every contour is a closed chain — each edge starting where the last one ended.</summary>
    public bool Validate()
    {
        for (var c = 0; c < ContourCount; c++)
        {
            var edges = ContourEdges(c);
            if (edges.Length == 0) continue;

            var corner = edges[^1].EndPoint;
            foreach (ref var edge in edges)
            {
                if (edge.P0 != corner) return false;
                corner = edge.EndPoint;
            }
        }

        return true;
    }

    /// <summary>Shifts every control point vertically, used to anchor the outline to the baseline.</summary>
    public void TranslateY(double delta)
    {
        if (delta == 0) return;
        var offset = new Vector2d(0, delta);

        foreach (ref var edge in Edges)
        {
            edge.P0 += offset;
            edge.P1 += offset;
            edge.P2 += offset;
            edge.P3 += offset;
        }
    }

    public void Bound(ref double l, ref double b, ref double r, ref double t)
    {
        foreach (ref var edge in Edges) edge.Bound(ref l, ref b, ref r, ref t);
    }

    public Bounds GetBounds()
    {
        var bounds = Bounds.Empty;
        Bound(ref bounds.L, ref bounds.B, ref bounds.R, ref bounds.T);
        return bounds;
    }

    /// <summary>
    ///     Signed area of a contour, by the shoelace formula over its end points. The sign gives
    ///     the winding direction: which way round the contour is drawn.
    /// </summary>
    public double ContourSignedArea(int contour)
    {
        var edges = ContourEdges(contour);
        double total = 0;

        foreach (ref var edge in edges)
        {
            var a = edge.P0;
            var b = edge.EndPoint;
            total += Vector2d.Cross(a, b);
        }

        return 0.5 * total;
    }
}

internal struct Bounds
{
    public double L;
    public double B;
    public double R;
    public double T;

    /// <summary>
    ///     An inverted box, seeded so that the first real coordinate wins every comparison. The
    ///     ±1e240 sentinels leave room to keep arithmetic on them finite.
    /// </summary>
    public static Bounds Empty => new() { L = 1e240, B = 1e240, R = -1e240, T = -1e240 };

    public readonly override string ToString()
    {
        return $"L={L:R} B={B:R} R={R:R} T={T:R}";
    }
}
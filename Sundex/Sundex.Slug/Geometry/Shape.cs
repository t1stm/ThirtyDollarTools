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
        for (var contour = 0; contour < ContourCount; contour++)
        {
            var edges = ContourEdges(contour);
            if (edges.Length == 0) continue;

            var corner = edges[^1].EndPoint;
            foreach (ref var edge in edges)
            {
                if (edge.Point0 != corner) return false;
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
            edge.Point0 += offset;
            edge.Point1 += offset;
            edge.Point2 += offset;
            edge.Point3 += offset;
        }
    }

    public void Bound(ref double left, ref double bottom, ref double right, ref double top)
    {
        foreach (ref var edge in Edges) edge.Bound(ref left, ref bottom, ref right, ref top);
    }

    public Bounds GetBounds()
    {
        var bounds = Bounds.Empty;
        Bound(ref bounds.Left, ref bounds.Bottom, ref bounds.Right, ref bounds.Top);
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
            var start = edge.Point0;
            var end = edge.EndPoint;
            total += Vector2d.Cross(start, end);
        }

        return 0.5 * total;
    }
}

internal struct Bounds
{
    public double Left;
    public double Bottom;
    public double Right;
    public double Top;

    /// <summary>
    ///     An inverted box, seeded so that the first real coordinate wins every comparison. The
    ///     ±1e240 sentinels leave room to keep arithmetic on them finite.
    /// </summary>
    public static Bounds Empty => new() { Left = 1e240, Bottom = 1e240, Right = -1e240, Top = -1e240 };

    public readonly override string ToString()
    {
        return $"L={Left:R} B={Bottom:R} R={Right:R} T={Top:R}";
    }
}
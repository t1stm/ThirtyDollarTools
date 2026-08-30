using System.Buffers;
using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Coloring;

/// <summary>
///     Assigns each edge of a shape two of the three output channels.
///     <para>
///         The pair switches at every corner, so two edges meeting at a corner share exactly one
///         channel. The shader's median-of-three then reconstructs a sharp vertex where a single
///         distance field would round it off.
///     </para>
/// </summary>
internal static class EdgeColoring
{
    /// <summary>Seeds which colour each contour starts on, and which way the pairs rotate.</summary>
    public const ulong DefaultSeed = 0;

    /// <summary>
    ///     Colours every edge of <paramref name="shape" />. Two edges count as meeting at a corner
    ///     when the angle between their tangents exceeds <paramref name="angleThreshold" />
    ///     radians; below it the join is treated as smooth and the colour carries through.
    /// </summary>
    public static void Simple(Shape shape, double angleThreshold, ulong seed = DefaultSeed)
    {
        var crossThreshold = Math.Sin(angleThreshold);
        var color = InitColor(ref seed);

        // Corner indices for the contour being coloured, thrown away at the end of each one.
        // Most contours fit the stack buffer; the rest borrow rather than allocate (Lato's widest
        // contour is 72 edges, so the overflow is real but rare).
        Span<int> corners = stackalloc int[64];
        var cornerBuffer = corners;
        int[]? rented = null;

        try
        {
            for (var c = 0; c < shape.ContourCount; c++)
            {
                var edges = shape.ContourEdges(c);
                if (edges.Length == 0) continue;

                if (edges.Length > cornerBuffer.Length)
                {
                    if (rented != null) ArrayPool<int>.Shared.Return(rented);
                    rented = ArrayPool<int>.Shared.Rent(edges.Length);
                    cornerBuffer = rented;
                }

                // Identify corners.
                var cornerCount = 0;
                var prevDirection = edges[^1].Direction(1);
                for (var i = 0; i < edges.Length; i++)
                {
                    if (IsCorner(prevDirection.Normalize(), edges[i].Direction(0).Normalize(), crossThreshold))
                        cornerBuffer[cornerCount++] = i;
                    prevDirection = edges[i].Direction(1);
                }

                switch (cornerCount)
                {
                    // Smooth contour — one colour throughout.
                    case 0:
                    {
                        SwitchColor(ref color, ref seed);
                        foreach (ref var edge in edges) edge.Color = color;
                        break;
                    }

                    // A teardrop — one corner only, so instead of alternating a pair, all three
                    // colours are spread around the loop. Shape.Normalize has already guaranteed
                    // there are at least three edges to spread them over.
                    case 1:
                    {
                        Span<EdgeColor> colors = stackalloc EdgeColor[3];
                        SwitchColor(ref color, ref seed);
                        colors[0] = color;
                        colors[1] = EdgeColor.White;
                        SwitchColor(ref color, ref seed);
                        colors[2] = color;

                        var corner = cornerBuffer[0];
                        var m = edges.Length;
                        for (var i = 0; i < m; i++)
                            edges[(corner + i) % m].Color =
                                colors[1 + (int)(3 + 2.875 * i / (m - 1) - 1.4375 + .5) - 3];
                        break;
                    }

                    // Multiple corners — switch colour at each one, banning the initial colour on the
                    // last spline so the loop does not close on a repeat.
                    default:
                    {
                        var spline = 0;
                        var start = cornerBuffer[0];
                        var m = edges.Length;

                        SwitchColor(ref color, ref seed);
                        var initialColor = color;

                        for (var i = 0; i < m; i++)
                        {
                            var index = (start + i) % m;
                            if (spline + 1 < cornerCount && cornerBuffer[spline + 1] == index)
                            {
                                ++spline;
                                SwitchColor(ref color, ref seed,
                                    spline == cornerCount - 1 ? initialColor : EdgeColor.Black);
                            }

                            edges[index].Color = color;
                        }

                        break;
                    }
                }
            }
        }
        finally
        {
            if (rented != null) ArrayPool<int>.Shared.Return(rented);
        }
    }

    private static bool IsCorner(Vector2d aDir, Vector2d bDir, double crossThreshold)
    {
        return Vector2d.Dot(aDir, bDir) <= 0 || Math.Abs(Vector2d.Cross(aDir, bDir)) > crossThreshold;
    }

    private static int SeedExtract2(ref ulong seed)
    {
        var v = (int)seed & 1;
        seed >>= 1;
        return v;
    }

    private static int SeedExtract3(ref ulong seed)
    {
        var v = (int)(seed % 3);
        seed /= 3;
        return v;
    }

    private static EdgeColor InitColor(ref ulong seed)
    {
        ReadOnlySpan<EdgeColor> colors = [EdgeColor.Cyan, EdgeColor.Magenta, EdgeColor.Yellow];
        return colors[SeedExtract3(ref seed)];
    }

    private static void SwitchColor(ref EdgeColor color, ref ulong seed)
    {
        var shifted = (int)color << (1 + SeedExtract2(ref seed));
        color = (EdgeColor)((shifted | (shifted >> 3)) & (int)EdgeColor.White);
    }

    private static void SwitchColor(ref EdgeColor color, ref ulong seed, EdgeColor banned)
    {
        var combined = color & banned;
        if (combined is EdgeColor.Red or EdgeColor.Green or EdgeColor.Blue)
            color = combined ^ EdgeColor.White;
        else
            SwitchColor(ref color, ref seed);
    }
}
using System.Runtime.CompilerServices;

namespace Sundex.Slug.Geometry;

internal enum EdgeKind : byte
{
    Linear = 0,
    Quadratic = 1,
    Cubic = 2
}

/// <summary>
///     One line or Bézier segment of a contour.
///     <para>
///         All three degrees share this one struct and dispatch on <see cref="Kind" />, so a
///         glyph's hundreds of edges stay in one flat array of values rather than becoming
///         separate heap objects.
///     </para>
///     <para>Control points fill from P0 up: Linear uses P0..P1, Quadratic P0..P2, Cubic P0..P3.</para>
/// </summary>
internal struct EdgeSegment
{
    public Vector2d P0;
    public Vector2d P1;
    public Vector2d P2;
    public Vector2d P3;
    public EdgeKind Kind;

    public static EdgeSegment Linear(Vector2d p0, Vector2d p1)
    {
        return new EdgeSegment { Kind = EdgeKind.Linear, P0 = p0, P1 = p1 };
    }

    public static EdgeSegment Quadratic(Vector2d p0, Vector2d p1, Vector2d p2)
    {
        // A quadratic whose control point sits on an end point is a straight line.
        if (p1 == p0 || p1 == p2)
            return Linear(p0, p2);
        return new EdgeSegment { Kind = EdgeKind.Quadratic, P0 = p0, P1 = p1, P2 = p2 };
    }

    public static EdgeSegment Cubic(Vector2d p0, Vector2d p1, Vector2d p2, Vector2d p3)
    {
        // Likewise, a cubic with both control points on an end point is a straight line.
        if ((p1 == p0 || p1 == p3) && (p2 == p0 || p2 == p3))
            return Linear(p0, p3);
        return new EdgeSegment { Kind = EdgeKind.Cubic, P0 = p0, P1 = p1, P2 = p2, P3 = p3 };
    }

    /// <summary>The point the segment ends at, whichever control point that is for its kind.</summary>
    public readonly Vector2d EndPoint => Kind switch
    {
        EdgeKind.Linear => P1,
        EdgeKind.Quadratic => P2,
        _ => P3
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2d Mix(Vector2d a, Vector2d b, double t)
    {
        return a * (1 - t) + b * t;
    }

    /// <summary>The point at parameter <paramref name="t" /> along the segment, 0 at the start, 1 at the end.</summary>
    public readonly Vector2d Point(double t)
    {
        return Kind switch
        {
            EdgeKind.Linear => Mix(P0, P1, t),
            EdgeKind.Quadratic => Mix(Mix(P0, P1, t), Mix(P1, P2, t), t),
            _ => CubicPoint(t)
        };
    }

    private readonly Vector2d CubicPoint(double t)
    {
        var p12 = Mix(P1, P2, t);
        return Mix(Mix(Mix(P0, P1, t), p12, t), Mix(p12, Mix(P2, P3, t), t), t);
    }

    /// <summary>
    ///     Grows the given bounding box to contain this segment. Curves contribute their
    ///     extrema as well as their end points, so the box is tight rather than a hull of the
    ///     control polygon.
    /// </summary>
    public readonly void Bound(ref double l, ref double b, ref double r, ref double t)
    {
        PointBounds(P0, ref l, ref b, ref r, ref t);
        PointBounds(EndPoint, ref l, ref b, ref r, ref t);

        switch (Kind)
        {
            case EdgeKind.Linear:
                return;

            case EdgeKind.Quadratic:
            {
                var bot = P1 - P0 - (P2 - P1);
                if (bot.X != 0)
                {
                    var param = (P1.X - P0.X) / bot.X;
                    if (param > 0 && param < 1) PointBounds(Point(param), ref l, ref b, ref r, ref t);
                }

                if (bot.Y != 0)
                {
                    var param = (P1.Y - P0.Y) / bot.Y;
                    if (param > 0 && param < 1) PointBounds(Point(param), ref l, ref b, ref r, ref t);
                }

                return;
            }

            default:
            {
                var a0 = P1 - P0;
                var a1 = 2 * (P2 - P1 - a0);
                var a2 = P3 - 3 * P2 + 3 * P1 - P0;

                Span<double> parameters = stackalloc double[2];

                var solutions = EquationSolver.SolveQuadratic(parameters, a2.X, a1.X, a0.X);
                for (var i = 0; i < solutions; ++i)
                    if (parameters[i] > 0 && parameters[i] < 1)
                        PointBounds(Point(parameters[i]), ref l, ref b, ref r, ref t);

                solutions = EquationSolver.SolveQuadratic(parameters, a2.Y, a1.Y, a0.Y);
                for (var i = 0; i < solutions; ++i)
                    if (parameters[i] > 0 && parameters[i] < 1)
                        PointBounds(Point(parameters[i]), ref l, ref b, ref r, ref t);

                return;
            }
        }
    }

    private static void PointBounds(Vector2d p, ref double l, ref double b, ref double r, ref double t)
    {
        if (p.X < l) l = p.X;
        if (p.Y < b) b = p.Y;
        if (p.X > r) r = p.X;
        if (p.Y > t) t = p.Y;
    }
}

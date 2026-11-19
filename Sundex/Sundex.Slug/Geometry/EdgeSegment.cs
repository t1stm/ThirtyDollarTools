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
    public Vector2d Point0;
    public Vector2d Point1;
    public Vector2d Point2;
    public Vector2d Point3;
    public EdgeKind Kind;

    public static EdgeSegment Linear(Vector2d point0, Vector2d point1)
    {
        return new EdgeSegment { Kind = EdgeKind.Linear, Point0 = point0, Point1 = point1 };
    }

    public static EdgeSegment Quadratic(Vector2d point0, Vector2d point1, Vector2d point2)
    {
        // A quadratic whose control point sits on an end point is a straight line.
        if (point1 == point0 || point1 == point2)
            return Linear(point0, point2);
        return new EdgeSegment { Kind = EdgeKind.Quadratic, Point0 = point0, Point1 = point1, Point2 = point2 };
    }

    public static EdgeSegment Cubic(Vector2d point0, Vector2d point1, Vector2d point2, Vector2d point3)
    {
        // Likewise, a cubic with both control points on an end point is a straight line.
        if ((point1 == point0 || point1 == point3) && (point2 == point0 || point2 == point3))
            return Linear(point0, point3);
        return new EdgeSegment { Kind = EdgeKind.Cubic, Point0 = point0, Point1 = point1, Point2 = point2, Point3 = point3 };
    }

    /// <summary>The point the segment ends at, whichever control point that is for its kind.</summary>
    public readonly Vector2d EndPoint => Kind switch
    {
        EdgeKind.Linear => Point1,
        EdgeKind.Quadratic => Point2,
        _ => Point3
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2d Mix(Vector2d from, Vector2d to, double progress)
    {
        return from * (1 - progress) + to * progress;
    }

    /// <summary>The point at parameter <paramref name="progress" /> along the segment, 0 at the start, 1 at the end.</summary>
    public readonly Vector2d Point(double progress)
    {
        return Kind switch
        {
            EdgeKind.Linear => Mix(Point0, Point1, progress),
            EdgeKind.Quadratic => Mix(Mix(Point0, Point1, progress), Mix(Point1, Point2, progress), progress),
            _ => CubicPoint(progress)
        };
    }

    private readonly Vector2d CubicPoint(double progress)
    {
        var p12 = Mix(Point1, Point2, progress);
        return Mix(Mix(Mix(Point0, Point1, progress), p12, progress), Mix(p12, Mix(Point2, Point3, progress), progress), progress);
    }

    /// <summary>
    ///     Grows the given bounding box to contain this segment. Curves contribute their
    ///     extrema as well as their end points, so the box is tight rather than a hull of the
    ///     control polygon.
    /// </summary>
    public readonly void Bound(ref double left, ref double bottom, ref double right, ref double top)
    {
        PointBounds(Point0, ref left, ref bottom, ref right, ref top);
        PointBounds(EndPoint, ref left, ref bottom, ref right, ref top);

        switch (Kind)
        {
            case EdgeKind.Linear:
                return;

            case EdgeKind.Quadratic:
            {
                var bot = Point1 - Point0 - (Point2 - Point1);
                if (bot.X != 0)
                {
                    var param = (Point1.X - Point0.X) / bot.X;
                    if (param > 0 && param < 1) PointBounds(Point(param), ref left, ref bottom, ref right, ref top);
                }

                if (bot.Y != 0)
                {
                    var param = (Point1.Y - Point0.Y) / bot.Y;
                    if (param > 0 && param < 1) PointBounds(Point(param), ref left, ref bottom, ref right, ref top);
                }

                return;
            }

            default:
            {
                var coefficient0 = Point1 - Point0;
                var coefficient1 = 2 * (Point2 - Point1 - coefficient0);
                var coefficient2 = Point3 - 3 * Point2 + 3 * Point1 - Point0;

                Span<double> parameters = stackalloc double[2];

                var solutions = EquationSolver.SolveQuadratic(parameters, coefficient2.X, coefficient1.X, coefficient0.X);
                for (var index = 0; index < solutions; ++index)
                    if (parameters[index] > 0 && parameters[index] < 1)
                        PointBounds(Point(parameters[index]), ref left, ref bottom, ref right, ref top);

                solutions = EquationSolver.SolveQuadratic(parameters, coefficient2.Y, coefficient1.Y, coefficient0.Y);
                for (var index = 0; index < solutions; ++index)
                    if (parameters[index] > 0 && parameters[index] < 1)
                        PointBounds(Point(parameters[index]), ref left, ref bottom, ref right, ref top);

                return;
            }
        }
    }

    private static void PointBounds(Vector2d point, ref double left, ref double bottom, ref double right, ref double top)
    {
        if (point.X < left) left = point.X;
        if (point.Y < bottom) bottom = point.Y;
        if (point.X > right) right = point.X;
        if (point.Y > top) top = point.Y;
    }
}

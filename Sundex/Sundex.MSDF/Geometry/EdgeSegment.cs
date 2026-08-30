using System.Runtime.CompilerServices;

namespace Sundex.MSDF.Geometry;

internal enum EdgeKind : byte
{
    Linear = 0,
    Quadratic = 1,
    Cubic = 2
}

/// <summary>The output channels an edge contributes its distance to.</summary>
[Flags]
internal enum EdgeColor : byte
{
    Black = 0,
    Red = 1,
    Green = 2,
    Yellow = 3,
    Blue = 4,
    Magenta = 5,
    Cyan = 6,
    White = 7
}

/// <summary>
///     One line or Bézier segment of a contour, and the distance maths for it.
///     <para>
///         All three degrees share this one struct and dispatch on <see cref="Kind" />, so a
///         glyph's hundreds of edges stay in one flat array of values rather than becoming
///         separate heap objects walked once per pixel.
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
    public EdgeColor Color;

    public static EdgeSegment Linear(Vector2d p0, Vector2d p1, EdgeColor color = EdgeColor.White)
    {
        return new EdgeSegment { Kind = EdgeKind.Linear, P0 = p0, P1 = p1, Color = color };
    }

    public static EdgeSegment Quadratic(Vector2d p0, Vector2d p1, Vector2d p2, EdgeColor color = EdgeColor.White)
    {
        // A quadratic whose control point sits on an end point is a straight line.
        if (p1 == p0 || p1 == p2)
            return Linear(p0, p2, color);
        return new EdgeSegment { Kind = EdgeKind.Quadratic, P0 = p0, P1 = p1, P2 = p2, Color = color };
    }

    public static EdgeSegment Cubic(Vector2d p0, Vector2d p1, Vector2d p2, Vector2d p3,
        EdgeColor color = EdgeColor.White)
    {
        // Likewise, a cubic with both control points on an end point is a straight line.
        if ((p1 == p0 || p1 == p3) && (p2 == p0 || p2 == p3))
            return Linear(p0, p3, color);
        return new EdgeSegment { Kind = EdgeKind.Cubic, P0 = p0, P1 = p1, P2 = p2, P3 = p3, Color = color };
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

    /// <summary>+1 for positive, -1 for zero or negative — a sign that is never 0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double NonZeroSign(double n)
    {
        return n > 0 ? 1 : -1;
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
    ///     The tangent at parameter <paramref name="t" />, not normalised. Where a curve's tangent
    ///     vanishes because its control points coincide, the chord through them stands in for it.
    /// </summary>
    public readonly Vector2d Direction(double t)
    {
        switch (Kind)
        {
            case EdgeKind.Linear:
                return P1 - P0;

            case EdgeKind.Quadratic:
            {
                var tangent = Mix(P1 - P0, P2 - P1, t);
                return tangent == Vector2d.Zero ? P2 - P0 : tangent;
            }

            default:
            {
                var tangent = Mix(Mix(P1 - P0, P2 - P1, t), Mix(P2 - P1, P3 - P2, t), t);
                if (tangent != Vector2d.Zero) return tangent;
                if (t == 0) return P2 - P0;
                if (t == 1) return P3 - P1;
                return tangent;
            }
        }
    }

    /// <summary>The second derivative at parameter <paramref name="t" /> — how fast the tangent is turning.</summary>
    public readonly Vector2d DirectionChange(double t)
    {
        return Kind switch
        {
            EdgeKind.Linear => Vector2d.Zero,
            EdgeKind.Quadratic => P2 - P1 - (P1 - P0),
            _ => Mix(P2 - P1 - (P1 - P0), P3 - P2 - (P2 - P1), t)
        };
    }

    /// <summary>
    ///     Distance from <paramref name="origin" /> to the nearest point of the segment, signed
    ///     negative on the left of the direction of travel and positive on the right.
    ///     <paramref name="param" /> comes back as the parameter of that nearest point, which
    ///     falls outside 0..1 when the projection lands beyond an end point.
    /// </summary>
    public readonly SignedDistance SignedDistance(Vector2d origin, out double param)
    {
        return Kind switch
        {
            EdgeKind.Linear => LinearSignedDistance(origin, out param),
            EdgeKind.Quadratic => QuadraticSignedDistance(origin, out param),
            _ => CubicSignedDistance(origin, out param)
        };
    }

    private readonly SignedDistance LinearSignedDistance(Vector2d origin, out double param)
    {
        var aq = origin - P0;
        var ab = P1 - P0;
        param = Vector2d.Dot(aq, ab) / Vector2d.Dot(ab, ab);
        var eq = (param > .5 ? P1 : P0) - origin;
        var endpointDistance = eq.Length;

        if (param > 0 && param < 1)
        {
            var orthoDistance = Vector2d.Dot(ab.GetOrthonormal(false), aq);
            if (Math.Abs(orthoDistance) < endpointDistance)
                return new SignedDistance(orthoDistance, 0);
        }

        return new SignedDistance(
            NonZeroSign(Vector2d.Cross(aq, ab)) * endpointDistance,
            Math.Abs(Vector2d.Dot(ab.Normalize(), eq.Normalize())));
    }

    private readonly SignedDistance QuadraticSignedDistance(Vector2d origin, out double param)
    {
        var qa = P0 - origin;
        var ab = P1 - P0;
        var br = P2 - P1 - ab;

        var a = Vector2d.Dot(br, br);
        var b = 3 * Vector2d.Dot(ab, br);
        var c = 2 * Vector2d.Dot(ab, ab) + Vector2d.Dot(qa, br);
        var d = Vector2d.Dot(qa, ab);

        Span<double> t = stackalloc double[3];
        var solutions = EquationSolver.SolveCubic(t, a, b, c, d);

        var epDir = Direction(0);
        var minDistance = NonZeroSign(Vector2d.Cross(epDir, qa)) * qa.Length; // distance from A
        param = -Vector2d.Dot(qa, epDir) / Vector2d.Dot(epDir, epDir);
        {
            epDir = Direction(1);
            var distance = (P2 - origin).Length; // distance from B
            if (distance < Math.Abs(minDistance))
            {
                minDistance = NonZeroSign(Vector2d.Cross(epDir, P2 - origin)) * distance;
                param = Vector2d.Dot(origin - P1, epDir) / Vector2d.Dot(epDir, epDir);
            }
        }

        for (var i = 0; i < solutions; ++i)
        {
            if (!(t[i] > 0) || !(t[i] < 1)) continue;

            var qe = qa + 2 * t[i] * ab + t[i] * t[i] * br;
            var distance = qe.Length;
            if (distance > Math.Abs(minDistance)) continue;

            minDistance = NonZeroSign(Vector2d.Cross(ab + t[i] * br, qe)) * distance;
            param = t[i];
        }

        if (param >= 0 && param <= 1)
            return new SignedDistance(minDistance, 0);

        return param < .5
            ? new SignedDistance(minDistance, Math.Abs(Vector2d.Dot(Direction(0).Normalize(), qa.Normalize())))
            : new SignedDistance(minDistance,
                Math.Abs(Vector2d.Dot(Direction(1).Normalize(), (P2 - origin).Normalize())));
    }

    // A cubic's nearest point has no closed form, so it is found by Newton refinement from a
    // handful of evenly spaced starting parameters — enough to land in every local minimum a
    // glyph curve has.
    private const int CubicSearchStarts = 4;
    private const int CubicSearchSteps = 4;

    private readonly SignedDistance CubicSignedDistance(Vector2d origin, out double param)
    {
        var qa = P0 - origin;
        var ab = P1 - P0;
        var br = P2 - P1 - ab;
        var @as = P3 - P2 - (P2 - P1) - br;

        var epDir = Direction(0);
        var minDistance = NonZeroSign(Vector2d.Cross(epDir, qa)) * qa.Length; // distance from A
        param = -Vector2d.Dot(qa, epDir) / Vector2d.Dot(epDir, epDir);
        {
            epDir = Direction(1);
            var distance = (P3 - origin).Length; // distance from B
            if (distance < Math.Abs(minDistance))
            {
                minDistance = NonZeroSign(Vector2d.Cross(epDir, P3 - origin)) * distance;
                param = Vector2d.Dot(epDir - (P3 - origin), epDir) / Vector2d.Dot(epDir, epDir);
            }
        }

        for (var i = 0; i <= CubicSearchStarts; ++i)
        {
            var t = (double)i / CubicSearchStarts;
            var qe = qa + 3 * t * ab + 3 * t * t * br + t * t * t * @as;

            for (var step = 0; step < CubicSearchSteps; ++step)
            {
                // Improve t
                var d1 = 3 * ab + 6 * t * br + 3 * t * t * @as;
                var d2 = 6 * br + 6 * t * @as;
                t -= Vector2d.Dot(qe, d1) / (Vector2d.Dot(d1, d1) + Vector2d.Dot(qe, d2));
                if (t <= 0 || t >= 1) break;

                qe = qa + 3 * t * ab + 3 * t * t * br + t * t * t * @as;
                var distance = qe.Length;
                if (!(distance < Math.Abs(minDistance))) continue;

                minDistance = NonZeroSign(Vector2d.Cross(d1, qe)) * distance;
                param = t;
            }
        }

        if (param >= 0 && param <= 1)
            return new SignedDistance(minDistance, 0);

        return param < .5
            ? new SignedDistance(minDistance, Math.Abs(Vector2d.Dot(Direction(0).Normalize(), qa.Normalize())))
            : new SignedDistance(minDistance,
                Math.Abs(Vector2d.Dot(Direction(1).Normalize(), (P3 - origin).Normalize())));
    }

    /// <summary>
    ///     For a point that projects past an end point, replaces the distance with the distance to
    ///     the segment's infinite extension. That keeps neighbouring edges' fields continuous
    ///     across the corner they share, which is what makes the corner come out sharp.
    /// </summary>
    public readonly void DistanceToPerpendicularDistance(ref SignedDistance distance, Vector2d origin, double param)
    {
        switch (param)
        {
            case < 0:
            {
                var dir = Direction(0).Normalize();
                var aq = origin - P0;
                var ts = Vector2d.Dot(aq, dir);
                if (ts >= 0) return;

                var perpendicularDistance = Vector2d.Cross(aq, dir);
                if (Math.Abs(perpendicularDistance) > Math.Abs(distance.Distance)) return;

                distance.Distance = perpendicularDistance;
                distance.Dot = 0;
                return;
            }
            case > 1:
            {
                var dir = Direction(1).Normalize();
                var bq = origin - EndPoint;
                var ts = Vector2d.Dot(bq, dir);
                if (ts <= 0) return;

                var perpendicularDistance = Vector2d.Cross(bq, dir);
                if (Math.Abs(perpendicularDistance) > Math.Abs(distance.Distance)) return;

                distance.Distance = perpendicularDistance;
                distance.Dot = 0;
                return;
            }
        }
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

    /// <summary>Flips the segment's direction of travel, which flips the sign of its distances.</summary>
    public void Reverse()
    {
        switch (Kind)
        {
            case EdgeKind.Linear:
                (P0, P1) = (P1, P0);
                break;
            case EdgeKind.Quadratic:
                (P0, P2) = (P2, P0);
                break;
            default:
                (P0, P3) = (P3, P0);
                (P1, P2) = (P2, P1);
                break;
        }
    }

    /// <summary>
    ///     Cuts the segment into three that trace the same curve, so a contour too short to carry
    ///     three edge colours can be given more edges. See <see cref="Shape.Normalize" />.
    /// </summary>
    public readonly void SplitInThirds(out EdgeSegment part0, out EdgeSegment part1, out EdgeSegment part2)
    {
        const double third = 1 / 3.0;
        const double twoThirds = 2 / 3.0;

        switch (Kind)
        {
            case EdgeKind.Linear:
                part0 = Linear(P0, Point(third), Color);
                part1 = Linear(Point(third), Point(twoThirds), Color);
                part2 = Linear(Point(twoThirds), P1, Color);
                return;

            case EdgeKind.Quadratic:
                part0 = Quadratic(P0, Mix(P0, P1, third), Point(third), Color);
                part1 = Quadratic(Point(third),
                    Mix(Mix(P0, P1, 5 / 9.0), Mix(P1, P2, 4 / 9.0), .5),
                    Point(twoThirds), Color);
                part2 = Quadratic(Point(twoThirds), Mix(P1, P2, twoThirds), P2, Color);
                return;

            default:
                part0 = Cubic(P0,
                    P0 == P1 ? P0 : Mix(P0, P1, third),
                    Mix(Mix(P0, P1, third), Mix(P1, P2, third), third),
                    Point(third), Color);
                part1 = Cubic(Point(third),
                    Mix(Mix(Mix(P0, P1, third), Mix(P1, P2, third), third),
                        Mix(Mix(P1, P2, third), Mix(P2, P3, third), third), twoThirds),
                    Mix(Mix(Mix(P0, P1, twoThirds), Mix(P1, P2, twoThirds), twoThirds),
                        Mix(Mix(P1, P2, twoThirds), Mix(P2, P3, twoThirds), twoThirds), third),
                    Point(twoThirds), Color);
                part2 = Cubic(Point(twoThirds),
                    Mix(Mix(P1, P2, twoThirds), Mix(P2, P3, twoThirds), twoThirds),
                    P2 == P3 ? P3 : Mix(P2, P3, twoThirds),
                    P3, Color);
                return;
        }
    }
}
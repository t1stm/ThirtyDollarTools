using Sundex.MSDF.Distance;
using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Tests;

/// <summary>
///     Signed distances on shapes whose geometry is known analytically, so a wrong answer is
///     arithmetic rather than a judgement call. Cubic root finding is the classic source of
///     silent wrongness here, so it gets its own cases.
/// </summary>
public class DistanceCoreTests
{
    private const double Tolerance = 1e-9;

    /// <summary>
    ///     Unit square from (0,0) to (1,1), wound <b>clockwise in Y-up</b> — the TrueType
    ///     convention for an outer contour, and the winding the solver reads as inside-positive.
    ///     Wind it the other way and every sign below flips.
    /// </summary>
    private static Shape UnitSquare()
    {
        var shape = new Shape();
        shape.BeginContour();
        shape.AddEdge(EdgeSegment.Linear(new Vector2d(0, 0), new Vector2d(0, 1)));
        shape.AddEdge(EdgeSegment.Linear(new Vector2d(0, 1), new Vector2d(1, 1)));
        shape.AddEdge(EdgeSegment.Linear(new Vector2d(1, 1), new Vector2d(1, 0)));
        shape.AddEdge(EdgeSegment.Linear(new Vector2d(1, 0), new Vector2d(0, 0)));
        shape.EndContour();
        return shape;
    }

    /// <summary>Unit-radius circle at the origin as four cubics, wound clockwise to match.</summary>
    private static Shape CircleApproximation()
    {
        const double k = 0.5522847498307936; // 4/3·(√2−1): the standard circular-arc cubic constant
        var shape = new Shape();
        shape.BeginContour();
        shape.AddEdge(EdgeSegment.Cubic(new Vector2d(1, 0), new Vector2d(1, -k), new Vector2d(k, -1),
            new Vector2d(0, -1)));
        shape.AddEdge(EdgeSegment.Cubic(new Vector2d(0, -1), new Vector2d(-k, -1), new Vector2d(-1, -k),
            new Vector2d(-1, 0)));
        shape.AddEdge(EdgeSegment.Cubic(new Vector2d(-1, 0), new Vector2d(-1, k), new Vector2d(-k, 1),
            new Vector2d(0, 1)));
        shape.AddEdge(EdgeSegment.Cubic(new Vector2d(0, 1), new Vector2d(k, 1), new Vector2d(1, k),
            new Vector2d(1, 0)));
        shape.EndContour();
        return shape;
    }

    [Theory]
    // Straight out from an edge midpoint: distance is the perpendicular drop.
    [InlineData(0.5, -0.25, -0.25)]
    [InlineData(1.25, 0.5, -0.25)]
    [InlineData(0.5, 1.5, -0.5)]
    [InlineData(-2.0, 0.5, -2.0)]
    // Inside: nearest wall.
    [InlineData(0.5, 0.5, 0.5)]
    [InlineData(0.1, 0.5, 0.1)]
    [InlineData(0.5, 0.9, 0.1)]
    // Diagonally past a corner: the perpendicular projection falls outside both edges, so the
    // answer is the corner distance, not either edge's line distance.
    [InlineData(-3.0, -4.0, -5.0)]
    [InlineData(4.0, 4.0, -4.242640687119285)]
    // Exactly on an edge and exactly on a corner.
    [InlineData(0.5, 0.0, 0.0)]
    [InlineData(0.0, 0.0, 0.0)]
    public void SquareTrueDistance(double x, double y, double expected)
    {
        var shape = UnitSquare();
        var finder = new ShapeDistanceFinder();
        finder.Reset(shape.EdgeCount);

        Assert.Equal(expected, finder.TrueDistance(shape, new Vector2d(x, y)), Tolerance);
    }

    /// <summary>
    ///     The cubic approximation is within ~2e-4 of a true circle, so distances are checked to
    ///     that, not to machine precision. What this really pins is the Newton search in
    ///     <c>EdgeSegment.CubicSignedDistance</c> converging at all, from every start.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0, 1.0)] // centre
    [InlineData(2.0, 0.0, -1.0)] // straight out along an endpoint
    [InlineData(0.0, -3.0, -2.0)]
    [InlineData(0.5, 0.0, 0.5)] // inside
    [InlineData(3.0, 4.0, -4.0)] // far out on a diagonal: |(3,4)| − 1
    [InlineData(0.6, 0.8, 0.0)] // on the circle
    public void CircleTrueDistanceApproximatesRadius(double x, double y, double expected)
    {
        var shape = CircleApproximation();
        var finder = new ShapeDistanceFinder();
        finder.Reset(shape.EdgeCount);

        Assert.Equal(expected, finder.TrueDistance(shape, new Vector2d(x, y)), 3e-4);
    }

    /// <summary>Distance must be sign-symmetric under contour reversal — this is what winding means.</summary>
    [Fact]
    public void ReversingTheContourFlipsTheSign()
    {
        var shape = UnitSquare();
        var finder = new ShapeDistanceFinder();

        finder.Reset(shape.EdgeCount);
        var inside = finder.TrueDistance(shape, new Vector2d(0.5, 0.5));
        finder.Reset(shape.EdgeCount);
        var outside = finder.TrueDistance(shape, new Vector2d(0.5, -0.25));

        shape.ReverseAllContours();

        finder.Reset(shape.EdgeCount);
        Assert.Equal(-inside, finder.TrueDistance(shape, new Vector2d(0.5, 0.5)), Tolerance);
        finder.Reset(shape.EdgeCount);
        Assert.Equal(-outside, finder.TrueDistance(shape, new Vector2d(0.5, -0.25)), Tolerance);
    }

    /// <summary>
    ///     With every edge coloured white all three channels see every edge, so within an edge's
    ///     angular domain each channel must reduce to the plain true distance. Any divergence
    ///     here is a bug in the perpendicular path.
    /// </summary>
    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(0.5, -0.25)]
    [InlineData(-2.0, 0.5)]
    [InlineData(0.1, 0.9)]
    public void InsideAnEdgeDomainEveryChannelEqualsTheTrueDistance(double x, double y)
    {
        var shape = UnitSquare();
        var finder = new ShapeDistanceFinder();
        var p = new Vector2d(x, y);

        finder.Reset(shape.EdgeCount);
        var expected = finder.TrueDistance(shape, p);

        finder.Reset(shape.EdgeCount);
        var multi = finder.MultiDistance(shape, p);

        Assert.Equal(expected, multi.R, Tolerance);
        Assert.Equal(expected, multi.G, Tolerance);
        Assert.Equal(expected, multi.B, Tolerance);
    }

    /// <summary>
    ///     Past a corner the channels deliberately <i>diverge</i> from the true distance: they
    ///     report the distance to the nearer edge's infinite extension instead of to the vertex.
    ///     That substitution is the entire mechanism that keeps MSDF corners sharp, so it is
    ///     worth pinning rather than treating as an inconsistency.
    /// </summary>
    [Theory]
    // Diagonally past (0,0): true distance is the 5-unit corner distance, perpendicular is 4.
    [InlineData(-3.0, -4.0, -5.0, -4.0)]
    // Diagonally past (1,1): true distance is 3√2, perpendicular is 3 to either extension.
    [InlineData(4.0, 4.0, -4.242640687119285, -3.0)]
    public void PastACornerTheChannelsUsePerpendicularDistance(double x, double y, double trueDistance,
        double perpendicular)
    {
        var shape = UnitSquare();
        var finder = new ShapeDistanceFinder();
        var p = new Vector2d(x, y);

        finder.Reset(shape.EdgeCount);
        Assert.Equal(trueDistance, finder.TrueDistance(shape, p), Tolerance);

        finder.Reset(shape.EdgeCount);
        var multi = finder.MultiDistance(shape, p);

        Assert.Equal(perpendicular, multi.R, Tolerance);
        Assert.Equal(perpendicular, multi.G, Tolerance);
        Assert.Equal(perpendicular, multi.B, Tolerance);
    }

    [Fact]
    public void SolveQuadraticHandlesTwoRootsOneRootAndNone()
    {
        Span<double> x = stackalloc double[2];

        // x² − 3x + 2 → 1, 2
        Assert.Equal(2, EquationSolver.SolveQuadratic(x, 1, -3, 2));
        Assert.Equal([1.0, 2.0], Sorted(x, 2), Comparer());

        // x² − 2x + 1 → double root at 1
        Assert.Equal(1, EquationSolver.SolveQuadratic(x, 1, -2, 1));
        Assert.Equal(1.0, x[0], Tolerance);

        // x² + 1 → no real roots
        Assert.Equal(0, EquationSolver.SolveQuadratic(x, 1, 0, 1));

        // Degenerates to linear: 2x − 4 → 2
        Assert.Equal(1, EquationSolver.SolveQuadratic(x, 0, 2, -4));
        Assert.Equal(2.0, x[0], Tolerance);

        // 0 == 0 is "any x", signalled with -1.
        Assert.Equal(-1, EquationSolver.SolveQuadratic(x, 0, 0, 0));
    }

    [Fact]
    public void SolveCubicHandlesThreeRealRootsAndDoubleRoots()
    {
        Span<double> x = stackalloc double[3];

        // (x−1)(x−2)(x−3) = x³ − 6x² + 11x − 6 — the three-distinct-real-roots branch.
        Assert.Equal(3, EquationSolver.SolveCubic(x, 1, -6, 11, -6));
        Assert.Equal([1.0, 2.0, 3.0], Sorted(x, 3), Comparer());

        // (x−1)²(x+2) = x³ − 3x + 2 — a double root, which comes back as two roots, not three.
        var count = EquationSolver.SolveCubic(x, 1, 0, -3, 2);
        Assert.Equal(2, count);
        Assert.Equal([-2.0, 1.0], Sorted(x, count), Comparer());

        // x³ − 1: one real root at 1, two complex.
        Assert.Equal(1, EquationSolver.SolveCubic(x, 1, 0, 0, -1));
        Assert.Equal(1.0, x[0], Tolerance);

        // Degenerates to the quadratic solver when a == 0.
        Assert.Equal(2, EquationSolver.SolveCubic(x, 0, 1, -3, 2));
        Assert.Equal([1.0, 2.0], Sorted(x, 2), Comparer());
    }

    private static double[] Sorted(Span<double> x, int count) => x[..count].ToArray().Order().ToArray();

    private static IEqualityComparer<double> Comparer() =>
        EqualityComparer<double>.Create((a, b) => Math.Abs(a - b) < 1e-9);
}

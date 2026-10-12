namespace Sundex.Slug.Geometry;

/// <summary>
///     Closed-form real roots of a quadratic: where a cubic edge's derivative is zero, for its
///     bounds.
///     <para>
///         Drops to a linear equation when the leading coefficient is negligible against the
///         others rather than dividing by it, which keeps near-degenerate curves accurate.
///     </para>
/// </summary>
internal static class EquationSolver
{
    private const double TooLargeRatio = 1e12;

    /// <summary>Solves ax² + bx + c = 0. Returns the root count, or -1 for "any x" (0 == 0).</summary>
    public static int SolveQuadratic(Span<double> x, double a, double b, double c)
    {
        // a == 0 -> linear equation
        if (a == 0 || Math.Abs(b) + Math.Abs(c) > TooLargeRatio * Math.Abs(a))
        {
            // a == 0, b == 0 -> no solution
            if (b == 0 || Math.Abs(c) > TooLargeRatio * Math.Abs(b))
                return c == 0 ? -1 : 0;

            x[0] = -c / b;
            return 1;
        }

        var dscr = b * b - 4 * a * c;
        switch (dscr)
        {
            case > 0:
                dscr = Math.Sqrt(dscr);
                x[0] = (-b + dscr) / (2 * a);
                x[1] = (-b - dscr) / (2 * a);
                return 2;
            case 0:
                x[0] = -b / (2 * a);
                return 1;
            default:
                return 0;
        }
    }
}

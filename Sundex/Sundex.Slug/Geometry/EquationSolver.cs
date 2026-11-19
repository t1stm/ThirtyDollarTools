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
    public static int SolveQuadratic(Span<double> x, double quadratic, double linear, double constant)
    {
        // a == 0 -> linear equation
        if (quadratic == 0 || Math.Abs(linear) + Math.Abs(constant) > TooLargeRatio * Math.Abs(quadratic))
        {
            // a == 0, b == 0 -> no solution
            if (linear == 0 || Math.Abs(constant) > TooLargeRatio * Math.Abs(linear))
                return constant == 0 ? -1 : 0;

            x[0] = -constant / linear;
            return 1;
        }

        var dscr = linear * linear - 4 * quadratic * constant;
        switch (dscr)
        {
            case > 0:
                dscr = Math.Sqrt(dscr);
                x[0] = (-linear + dscr) / (2 * quadratic);
                x[1] = (-linear - dscr) / (2 * quadratic);
                return 2;
            case 0:
                x[0] = -linear / (2 * quadratic);
                return 1;
            default:
                return 0;
        }
    }
}

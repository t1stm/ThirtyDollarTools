namespace Sundex.MSDF.Geometry;

/// <summary>
///     Closed-form real roots of the quadratics and cubics the curve maths reduces to.
///     <para>
///         Each solver drops to the next lower degree when its leading coefficient is negligible
///         against the others, rather than dividing by it. Those branches are what keep
///         near-degenerate curves accurate; folding them away leaves subtly wrong distances that
///         show up as soft glyph edges.
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

    /// <summary>Solves ax³ + bx² + cx + d = 0. Returns the root count, or -1 for "any x".</summary>
    public static int SolveCubic(Span<double> x, double a, double b, double c, double d)
    {
        if (a != 0)
        {
            var bn = b / a;
            // Above this ratio, the numerical error of the normed solver exceeds the value.
            if (Math.Abs(bn) < TooLargeRatio)
                return SolveCubicNormed(x, bn, c / a, d / a);
        }

        return SolveQuadratic(x, b, c, d);
    }

    /// <summary>Solves x³ + ax² + bx + c = 0.</summary>
    private static int SolveCubicNormed(Span<double> x, double a, double b, double c)
    {
        var a2 = a * a;
        var q = 1 / 9.0 * (a2 - 3 * b);
        var r = 1 / 54.0 * (a * (2 * a2 - 9 * b) + 27 * c);
        var r2 = r * r;
        var q3 = q * q * q;
        a *= 1 / 3.0;

        if (r2 < q3)
        {
            var t = r / Math.Sqrt(q3);
            t = Math.Clamp(t, -1, 1);
            t = Math.Acos(t);
            q = -2 * Math.Sqrt(q);
            x[0] = q * Math.Cos(1 / 3.0 * t) - a;
            x[1] = q * Math.Cos(1 / 3.0 * (t + 2 * Math.PI)) - a;
            x[2] = q * Math.Cos(1 / 3.0 * (t - 2 * Math.PI)) - a;
            return 3;
        }

        var u = (r < 0 ? 1 : -1) * Math.Pow(Math.Abs(r) + Math.Sqrt(r2 - q3), 1 / 3.0);
        var v = u == 0 ? 0 : q / u;
        x[0] = u + v - a;

        if (u != v && !(Math.Abs(u - v) < 1e-12 * Math.Abs(u + v))) return 1;

        x[1] = -0.5 * (u + v) - a;
        return 2;
    }
}

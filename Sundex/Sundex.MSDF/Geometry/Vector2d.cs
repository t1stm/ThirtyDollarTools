using System.Runtime.CompilerServices;

namespace Sundex.MSDF.Geometry;

/// <summary>
///     Double-precision 2D vector.
///     Double rather than float because the cubic root finding in the distance solver loses
///     curved-segment accuracy at single precision.
/// </summary>
internal readonly struct Vector2d(double x, double y) : IEquatable<Vector2d>
{
    public readonly double X = x;
    public readonly double Y = y;

    public static Vector2d Zero => new(0, 0);

    public double Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Math.Sqrt(X * X + Y * Y);
    }

    /// <summary>Unit vector in the same direction. A zero-length vector yields (0,1), or (0,0) when allowed.</summary>
    public Vector2d Normalize(bool allowZero = false)
    {
        var len = Length;
        if (len != 0) return new Vector2d(X / len, Y / len);
        return new Vector2d(0, allowZero ? 0 : 1);
    }

    /// <summary>Perpendicular vector of the same length; <paramref name="polarity" /> picks which of the two.</summary>
    public Vector2d GetOrthogonal(bool polarity = true)
    {
        return polarity ? new Vector2d(-Y, X) : new Vector2d(Y, -X);
    }

    /// <summary>Unit-length perpendicular vector; <paramref name="polarity" /> picks which of the two.</summary>
    public Vector2d GetOrthonormal(bool polarity = true, bool allowZero = true)
    {
        var len = Length;
        if (len == 0) return polarity ? new Vector2d(0, allowZero ? 0 : 1) : new Vector2d(0, -(allowZero ? 0 : 1));
        return polarity ? new Vector2d(-Y / len, X / len) : new Vector2d(Y / len, -X / len);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dot(Vector2d a, Vector2d b)
    {
        return a.X * b.X + a.Y * b.Y;
    }

    /// <summary>The 2D "cross product" — the z component of the 3D cross product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cross(Vector2d a, Vector2d b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    public static Vector2d operator +(Vector2d a, Vector2d b)
    {
        return new Vector2d(a.X + b.X, a.Y + b.Y);
    }

    public static Vector2d operator -(Vector2d a, Vector2d b)
    {
        return new Vector2d(a.X - b.X, a.Y - b.Y);
    }

    public static Vector2d operator -(Vector2d a)
    {
        return new Vector2d(-a.X, -a.Y);
    }

    public static Vector2d operator *(Vector2d a, double s)
    {
        return new Vector2d(a.X * s, a.Y * s);
    }

    public static Vector2d operator *(double s, Vector2d a)
    {
        return new Vector2d(a.X * s, a.Y * s);
    }

    public static Vector2d operator /(Vector2d a, double s)
    {
        return new Vector2d(a.X / s, a.Y / s);
    }

    /// <summary>Component-wise product.</summary>
    public static Vector2d Scale(Vector2d a, Vector2d b)
    {
        return new Vector2d(a.X * b.X, a.Y * b.Y);
    }

    public bool Equals(Vector2d other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y);
    }

    public override bool Equals(object? obj)
    {
        return obj is Vector2d o && Equals(o);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(Vector2d a, Vector2d b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Vector2d a, Vector2d b)
    {
        return !a.Equals(b);
    }

    public override string ToString()
    {
        return $"({X:R}, {Y:R})";
    }
}
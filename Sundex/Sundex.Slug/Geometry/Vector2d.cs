using System.Runtime.CompilerServices;

namespace Sundex.Slug.Geometry;

/// <summary>
///     Double-precision 2D vector, which outlines are built and measured in. The Slug build
///     narrows to float only when it packs the curves.
/// </summary>
internal readonly struct Vector2d(double x, double y) : IEquatable<Vector2d>
{
    public readonly double X = x;
    public readonly double Y = y;

    public double Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Math.Sqrt(X * X + Y * Y);
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
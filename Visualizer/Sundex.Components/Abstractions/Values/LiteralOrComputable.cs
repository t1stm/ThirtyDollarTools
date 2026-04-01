using System.Globalization;

namespace Sundex.Components.Abstractions.Values;

/// <summary>
///     Represents a literal pixel value or a percentage value.
///     When <see cref="IsPercentage" /> is true, <see cref="Value" /> is interpreted as percent (0-100).
/// </summary>
public readonly struct LiteralOrComputable(float value, bool isPercentage = false, bool auto = false) : IEquatable<LiteralOrComputable>
{
    public float Value { get; } = value;
    public bool IsPercentage { get; } = isPercentage;
    public bool Auto { get; } = auto;

    public static readonly LiteralOrComputable AutoSize = new(0, false, true);

    public static implicit operator LiteralOrComputable(float value)
    {
        return new LiteralOrComputable(value, false);
    }


    public float Resolve(float reference)
    {
        return IsPercentage ? reference * (Value / 100f) : Value;
    }

    public override string ToString()
    {
        return IsPercentage
            ? Value.ToString(CultureInfo.InvariantCulture) + "%"
            : Value.ToString(CultureInfo.InvariantCulture);
    }

    public static LiteralOrComputable Percent(float i)
    {
        return new LiteralOrComputable(i, true);
    }

    public override bool Equals(object? obj)
    {
        return obj is LiteralOrComputable other &&
               Math.Abs(Value - other.Value) < double.Epsilon &&
               IsPercentage == other.IsPercentage &&
               Auto == other.Auto;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, IsPercentage, Auto);
    }

    public static bool operator ==(LiteralOrComputable left, LiteralOrComputable right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(LiteralOrComputable left, LiteralOrComputable right)
    {
        return !left.Equals(right);
    }

    public bool Equals(LiteralOrComputable other)
    {
        return Value.Equals(other.Value) && IsPercentage == other.IsPercentage && Auto == other.Auto;
    }
}
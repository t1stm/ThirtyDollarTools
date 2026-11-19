using System.Globalization;

namespace Sundex.Components.Abstractions.Values;

/// <summary>
///     Represents a literal pixel value or a percentage value.
///     When <see cref="IsPercentage" /> is true, <see cref="Value" /> is interpreted as percent (0-100).
/// </summary>
public readonly struct LiteralOrComputable(float value, bool isPercentage = false, bool auto = false)
    : IEquatable<LiteralOrComputable>
{
    public float Value { get; } = value;
    public bool IsPercentage { get; } = isPercentage;
    public bool Auto { get; } = auto;

    public static readonly LiteralOrComputable AutoSize = new(0, false, true);

    /// <summary>Reads <c>auto</c>, <c>50%</c>, <c>12px</c> or <c>12</c>, as markup and stylesheets write them.</summary>
    public static LiteralOrComputable Parse(string text)
    {
        var value = text.Trim();
        if (value.Equals("auto", StringComparison.OrdinalIgnoreCase)) return AutoSize;
        var percent = value.EndsWith('%');
        if (percent) value = value[..^1];
        else if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase)) value = value[..^2];
        return new LiteralOrComputable(
            float.Parse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture),
            percent);
    }

    public static implicit operator LiteralOrComputable(float value)
    {
        return new LiteralOrComputable(value);
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

    public static LiteralOrComputable Percent(float percent)
    {
        return new LiteralOrComputable(percent, true);
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
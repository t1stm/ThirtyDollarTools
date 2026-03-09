using System.Globalization;

namespace Sundex.Components.Abstractions.Values;

/// <summary>
///     Represents a literal pixel value or a percentage value.
///     When <see cref="IsPercentage" /> is true, <see cref="Value" /> is interpreted as percent (0-100).
/// </summary>
public readonly struct LiteralOrComputable(float value, bool isPercentage, bool auto = false)
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
}
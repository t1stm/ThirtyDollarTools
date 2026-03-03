using System.Globalization;

namespace Sundex.Components.Abstractions.Values;

/// <summary>
/// Represents a literal pixel value or a percentage value.
/// When <see cref="IsPercentage"/> is true, <see cref="Value"/> is interpreted as percent (0-100).
/// </summary>
public readonly struct LiteralOrPercentage(float value, bool isPercentage)
{
    public float Value { get; } = value;
    public bool IsPercentage { get; } = isPercentage;

    public static implicit operator LiteralOrPercentage(float value)
        => new(value, false);

    
    public float Resolve(float reference) => IsPercentage ? reference * (Value / 100f) : Value;
    public override string ToString() => IsPercentage
        ? Value.ToString(CultureInfo.InvariantCulture) + "%"
        : Value.ToString(CultureInfo.InvariantCulture);
}

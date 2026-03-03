namespace Sundex.Style.DSL.Abstract.Values;

public record VectorValue(double X, double Y, double? Z = null, double? W = null) : IStyleValue
{
    public int Count { get; } = 2 + (Z != null ? 1 : 0) + (W != null ? 1 : 0);

    public VectorValue(ReadOnlySpan<NumberValue> values) : this(0, 0)
    {
        switch (values.Length)
        {
            case < 2: throw new ArgumentException("Vector must have at least 2 values.");
            case 2: (X, Y) = (values[0].Value, values[1].Value); break;
            case 3: (X, Y, Z) = (values[0].Value, values[1].Value, values[2].Value); break;
            case 4: (X, Y, Z, W) = (values[0].Value, values[1].Value, values[2].Value, values[3].Value); break;
            default: throw new ArgumentException("Vector must have at most 4 values.");
        }
    }

    object IStyleValue.Value => (X, Y, Z, W);

    public override string ToString() => $"vec{Count}(" + Count switch
    {
        2 => $"{X} {Y}",
        3 => $"{X} {Y} {Z}",
        4 => $"{X} {Y} {Z} {W}",
        _ => throw new InvalidOperationException()
    } + ")";
}
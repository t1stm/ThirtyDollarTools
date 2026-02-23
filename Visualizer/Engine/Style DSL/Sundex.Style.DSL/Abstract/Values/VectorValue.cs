namespace Sundex.Style.DSL.Abstract.Values;

public record VectorValue(double X, double Y) : IStyleValue
{
    object IStyleValue.Value => (X, Y);
    public override string ToString() => $"vec2({X}, {Y})";
}
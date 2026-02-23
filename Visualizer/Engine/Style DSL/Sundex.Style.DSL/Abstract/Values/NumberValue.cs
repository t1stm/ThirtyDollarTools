namespace Sundex.Style.DSL.Abstract.Values;

public record NumberValue(double Value, string Unit) : IStyleValue
{
    object IStyleValue.Value => Value;
    public override string ToString() => $"{Value}{Unit}";
}
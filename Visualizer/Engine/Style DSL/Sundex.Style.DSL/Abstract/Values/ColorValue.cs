namespace Sundex.Style.DSL.Abstract.Values;

public record ColorValue(string Value) : IStyleValue
{
    object IStyleValue.Value => Value;
    public override string ToString() => Value;
}
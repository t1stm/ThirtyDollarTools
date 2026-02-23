namespace Sundex.Style.DSL.Abstract.Values;

public record StringValue(string Value) : IStyleValue
{
    object IStyleValue.Value => Value;
    public override string ToString() => $"\"{Value}\"";
}
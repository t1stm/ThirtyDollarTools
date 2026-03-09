namespace Sundex.Style.DSL.Abstract.Values;

public record NumberValue(float Value, string Unit) : IStyleValue
{
    object IStyleValue.Value => Value;

    public override string ToString()
    {
        return $"{Value}{Unit}";
    }
}
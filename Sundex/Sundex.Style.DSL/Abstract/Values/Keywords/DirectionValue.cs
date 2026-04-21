namespace Sundex.Style.DSL.Abstract.Values.Keywords;

public record DirectionValue(IStyleValue Value) : IStyleValue
{
    object IStyleValue.Value => Value;

    public override string ToString()
    {
        return "!direction " + Value;
    }
}
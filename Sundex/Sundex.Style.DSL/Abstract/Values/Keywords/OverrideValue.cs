namespace Sundex.Style.DSL.Abstract.Values.Keywords;

public record OverrideValue(IStyleValue Value) : IStyleValue
{
    public BlockValue Properties => (BlockValue)Value;
    object IStyleValue.Value => Value;

    public override string ToString()
    {
        return "!override " + Value;
    }
}
namespace Sundex.Style.DSL.Abstract.Values.Keywords;

public record OverrideValue(IStyleValue Value) : IStyleValue
{
    object IStyleValue.Value => Value;
    public BlockValue Properties => (BlockValue)Value;
    public override string ToString() => "!override " + Value;
}

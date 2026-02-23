namespace Sundex.Style.DSL.Abstract.Values;

public record BlockValue(Dictionary<string, IStyleValue> Properties) : IStyleValue
{
    object IStyleValue.Value => Properties;
    public override string ToString() => "{ ... }";
}
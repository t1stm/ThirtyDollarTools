namespace Sundex.Style.DSL.Abstract.Values;

public record ArrayValue(List<IStyleValue> Values) : IStyleValue
{
    object IStyleValue.Value => Values;
    public override string ToString() => "[ ... ]";
}
namespace Sundex.Style.DSL.Abstract.Values;

public record MapValue(Dictionary<IStyleValue, IStyleValue> Values) : IStyleValue
{
    object IStyleValue.Value => Values;

    public override string ToString()
    {
        return "[ key=value, ... ]";
    }
}
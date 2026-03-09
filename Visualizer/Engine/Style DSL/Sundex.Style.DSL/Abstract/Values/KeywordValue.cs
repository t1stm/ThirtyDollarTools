namespace Sundex.Style.DSL.Abstract.Values;

public record KeywordValue(string Name) : IStyleValue
{
    object IStyleValue.Value => Name;

    public override string ToString()
    {
        return $"!{Name}";
    }
}
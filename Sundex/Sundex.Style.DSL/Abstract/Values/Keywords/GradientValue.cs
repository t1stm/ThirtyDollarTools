namespace Sundex.Style.DSL.Abstract.Values.Keywords;

public record GradientValue(IStyleValue Value) : IStyleValue
{
    // Native typed properties extracted from the inner block
    public string? Type { get; } = (Value as BlockValue)?.Properties.TryGetValue("type", out var value) == true
        ? value.Value as string
        : null;

    public IStyleValue? Direction { get; } =
        (Value as BlockValue)?.Properties.TryGetValue("direction", out var value) == true
            ? value
            : null;

    public List<StopsValue.GradientStop> Stops { get; } =
        Value is not BlockValue block || !block.Properties.TryGetValue("stops", out var value)
            ? []
            : value switch
            {
                StopsValue stopsValue => stopsValue.Stops,
                MapValue or ArrayValue => StopsValue.BuildStops(value),
                _ => []
            };

    object IStyleValue.Value => Value;

    public override string ToString()
    {
        return "!gradient " + Value;
    }
}
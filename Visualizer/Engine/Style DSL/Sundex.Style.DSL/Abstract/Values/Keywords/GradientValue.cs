using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Style.DSL.Abstract.Values.Keywords;

public record GradientValue(IStyleValue Value) : IStyleValue
{
    object IStyleValue.Value => Value;

    // Native typed properties extracted from the inner block
    public string? Type { get; } = (Value as BlockValue)?.Properties.TryGetValue("type", out var v) == true
        ? v.Value as string
        : null;
    
    public IStyleValue? Direction { get; } =
        (Value as BlockValue)?.Properties.TryGetValue("direction", out var v) == true
            ? v
            : null;

    public List<StopsValue.GradientStop> Stops { get; } =
        Value is not BlockValue block || !block.Properties.TryGetValue("stops", out var v)
            ? []
            : v switch
            {
                StopsValue sv => sv.Stops,
                MapValue or ArrayValue => StopsValue.BuildStops(v),
                _ => []
            };

    public override string ToString() => "!gradient " + Value;
}
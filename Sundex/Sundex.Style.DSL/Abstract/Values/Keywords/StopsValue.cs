namespace Sundex.Style.DSL.Abstract.Values.Keywords;

public record StopsValue(IStyleValue Value) : IStyleValue
{
    public IStyleValue Elements => Value;

    public List<GradientStop> Stops => BuildStops(Value);
    object IStyleValue.Value => Value;

    public override string ToString()
    {
        return "!stops " + Value;
    }

    internal static List<GradientStop> BuildStops(IStyleValue value)
    {
        var list = new List<GradientStop>();
        switch (value)
        {
            case MapValue map:
            {
                foreach (var kvp in map.Values)
                {
                    var pct = ExtractPercentage(kvp.Key);
                    if (kvp.Value is not ColorValue color)
                        throw new ArgumentException("Gradient stop must be a color value");
                    list.Add(new GradientStop(color, pct));
                }

                break;
            }
            case ArrayValue arr:
            {
                var count = arr.Values.Count;
                for (var index = 0; index < count; index++)
                {
                    var entry = arr.Values[index];
                    if (entry is not ColorValue color)
                        throw new ArgumentException("Gradient stop array items must be colors");
                    var pct = count <= 1 ? 0 : (float)index / (count - 1);
                    list.Add(new GradientStop(color, pct));
                }

                break;
            }
        }

        list.Sort((first, second) => first.Percentage.CompareTo(second.Percentage));
        return list;
    }

    private static float ExtractPercentage(IStyleValue key)
    {
        return key switch
        {
            NumberValue { Unit: "%" } num => num.Value / 100f,
            NumberValue num when string.IsNullOrEmpty(num.Unit) => num.Value,
            _ => throw new ArgumentException("Key for stops map must be a percentage number")
        };
    }

    public record GradientStop(ColorValue Color, float Percentage);
}
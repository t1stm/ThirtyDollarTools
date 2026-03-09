namespace Sundex.Style.DSL.Abstract.Values.Keywords;

public record StopsValue(IStyleValue Value) : IStyleValue
{
    object IStyleValue.Value => Value;
    public IStyleValue Elements => Value;

    public List<GradientStop> Stops => BuildStops(Value);
    public override string ToString() => "!stops " + Value;

    public record GradientStop(ColorValue Color, float Percentage);

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
                var n = arr.Values.Count;
                for (var i = 0; i < n; i++)
                {
                    var v = arr.Values[i];
                    if (v is not ColorValue color)
                        throw new ArgumentException("Gradient stop array items must be colors");
                    var pct = n <= 1 ? 0 : (float)i / (n - 1);
                    list.Add(new GradientStop(color, pct));
                }
                break;
            }
        }

        list.Sort((a, b) => a.Percentage.CompareTo(b.Percentage));
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
}

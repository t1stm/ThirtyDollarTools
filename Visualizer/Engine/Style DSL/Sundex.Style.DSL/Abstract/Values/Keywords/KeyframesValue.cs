namespace Sundex.Style.DSL.Abstract.Values.Keywords;

public record KeyframesValue(IStyleValue Value) : IStyleValue
{
    object IStyleValue.Value => Value;
    public IStyleValue Elements => Value;
    public List<KeyframeStep> Keyframes => BuildKeyframes(Value);

    public override string ToString() => "!keyframes " + Value;

    private static List<KeyframeStep> BuildKeyframes(IStyleValue value)
    {
        var list = new List<KeyframeStep>();
        switch (value)
        {
            case MapValue map:
            {
                foreach (var kvp in map.Values)
                {
                    var pct = ExtractPercentage(kvp.Key);
                    if (kvp.Value is not BlockValue block)
                        throw new ArgumentException("Keyframe value must be a block of properties");
                    list.Add(new KeyframeStep(pct, new Dictionary<string, IStyleValue>(block.Properties)));
                }
                break;
            }
            case ArrayValue arr:
            {
                var n = arr.Values.Count;
                for (var i = 0; i < n; i++)
                {
                    var v = arr.Values[i];
                    if (v is not BlockValue block)
                        throw new ArgumentException("Keyframe array items must be blocks");
                    var pct = n <= 1 ? 0 : 100.0 * i / (n - 1);
                    list.Add(new KeyframeStep(pct, new Dictionary<string, IStyleValue>(block.Properties)));
                }
                break;
            }
        }

        // Sort for stable access
        list.Sort((a, b) => a.Percentage.CompareTo(b.Percentage));
        return list;
    }

    private static double ExtractPercentage(IStyleValue key)
    {
        return key switch
        {
            NumberValue { Unit: "%" } num => num.Value,
            NumberValue num when string.IsNullOrEmpty(num.Unit) => num.Value,
            _ => throw new ArgumentException("Key for keyframes map must be a percentage number")
        };
    }

    public record KeyframeStep(double Percentage, Dictionary<string, IStyleValue> Properties);
}

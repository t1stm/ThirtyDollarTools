namespace Sundex.Style.DSL.Abstract.Values.Keywords;

public record KeyframesValue(IStyleValue Value) : IStyleValue
{
    public IStyleValue Elements => Value;
    public List<KeyframeStep> Keyframes => BuildKeyframes(Value);
    object IStyleValue.Value => Value;

    public override string ToString()
    {
        return "!keyframes " + Value;
    }

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
                        list.Add(new KeyframeStep(0, new Dictionary<string, IStyleValue>(block.Properties)));
                    }

                    break;
                }
        }

        if (value is not ArrayValue || list.Count <= 0) return list;
        var explicitPoints = new List<(int index, double pct)>();
        for (var i = 0; i < list.Count; i++)
            if (TryGetExplicitPercentage(list[i].Properties, out var pct))
                explicitPoints.Add((i, pct));

        if (explicitPoints.Count == 0 || explicitPoints[0].index != 0)
        {
            explicitPoints.Insert(0, (0, 0.0));
        }
        else
        {
            if (Math.Abs(explicitPoints[0].pct - 0.0) > double.Epsilon) explicitPoints[0] = (0, 0.0);
        }

        if (explicitPoints[^1].index != list.Count - 1)
            explicitPoints.Add((list.Count - 1, 1.0));
        else if (Math.Abs(explicitPoints[^1].pct - 1.0) > double.Epsilon)
            explicitPoints[^1] = (explicitPoints[^1].index, 1.0);

        for (var p = 0; p < explicitPoints.Count - 1; p++)
        {
            var (startIdx, startPct) = explicitPoints[p];
            var (endIdx, endPct) = explicitPoints[p + 1];
            var length = endIdx - startIdx;
            if (length <= 0) continue;

            list[startIdx] = list[startIdx] with { Percentage = startPct };
            list[endIdx] = list[endIdx] with { Percentage = endPct };

            var delta = (endPct - startPct) / length;
            for (var i = startIdx + 1; i < endIdx; i++)
            {
                var t = i - startIdx;
                var pct = startPct + delta * t;
                list[i] = list[i] with { Percentage = pct };
            }
        }

        return list;
    }

    private static bool TryGetExplicitPercentage(Dictionary<string, IStyleValue> props, out double pct)
    {
        // Common property names that may carry an explicit percentage for keyframes
        // Prefer exact match if available
        if (props.TryGetValue("%", out var v) ||
            props.TryGetValue("percent", out v) ||
            props.TryGetValue("percentage", out v))
            switch (v)
            {
                case NumberValue { Unit: "%" } nv:
                    pct = nv.Value / 100.0;
                    return true;
                case NumberValue nv2 when string.IsNullOrEmpty(nv2.Unit):
                    pct = nv2.Value;
                    return true;
            }

        pct = 0;
        return false;
    }

    private static double ExtractPercentage(IStyleValue key)
    {
        return key switch
        {
            NumberValue { Unit: "%" } num => num.Value / 100.0,
            NumberValue num when string.IsNullOrEmpty(num.Unit) => num.Value,
            _ => throw new ArgumentException("Key for keyframes map must be a percentage number")
        };
    }

    public record KeyframeStep(double Percentage, Dictionary<string, IStyleValue> Properties);
}
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
                var count = arr.Values.Count;
                for (var index = 0; index < count; index++)
                {
                    var entry = arr.Values[index];
                    if (entry is not BlockValue block)
                        throw new ArgumentException("Keyframe array items must be blocks");
                    list.Add(new KeyframeStep(0, new Dictionary<string, IStyleValue>(block.Properties)));
                }

                break;
            }
        }

        if (value is not ArrayValue || list.Count <= 0) return list;
        var explicitPoints = new List<(int index, double pct)>();
        for (var index = 0; index < list.Count; index++)
            if (TryGetExplicitPercentage(list[index].Properties, out var pct))
                explicitPoints.Add((index, pct));

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

        for (var segment = 0; segment < explicitPoints.Count - 1; segment++)
        {
            var (startIdx, startPct) = explicitPoints[segment];
            var (endIdx, endPct) = explicitPoints[segment + 1];
            var length = endIdx - startIdx;
            if (length <= 0) continue;

            list[startIdx] = list[startIdx] with { Percentage = startPct };
            list[endIdx] = list[endIdx] with { Percentage = endPct };

            var delta = (endPct - startPct) / length;
            for (var index = startIdx + 1; index < endIdx; index++)
            {
                var step = index - startIdx;
                var pct = startPct + delta * step;
                list[index] = list[index] with { Percentage = pct };
            }
        }

        return list;
    }

    private static bool TryGetExplicitPercentage(Dictionary<string, IStyleValue> props, out double pct)
    {
        // Common property names that may carry an explicit percentage for keyframes
        // Prefer exact match if available
        if (props.TryGetValue("%", out var value) ||
            props.TryGetValue("percent", out value) ||
            props.TryGetValue("percentage", out value))
            switch (value)
            {
                case NumberValue { Unit: "%" } numberValue:
                    pct = numberValue.Value / 100.0;
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
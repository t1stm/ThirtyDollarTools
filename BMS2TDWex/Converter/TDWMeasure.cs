namespace BMS2TDW.Converter;

public readonly struct TdwMeasure
{
    private readonly CombineStack[] _combineStacks;

    public TdwMeasure(int division)
    {
        _combineStacks = new CombineStack[division];
        for (var index = 0; index < _combineStacks.Length; index++) _combineStacks[index] = new CombineStack();
    }

    public void PlaceEvent(int sourceMeasure, int index, string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName)) return;

        var real_division = _combineStacks.Length / sourceMeasure;
        var real_index = index * real_division;

        var stack = _combineStacks[real_index];
        stack.Events.Add(eventName + '|');
    }

    public string Export()
    {
        return string.Concat(_combineStacks.Select(r => r.Export()));
    }
}
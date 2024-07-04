namespace BMS2TDW.Converter;

public readonly struct TDWMeasure
{
    private readonly CombineStack[] CombineStacks;

    public TDWMeasure(int division)
    {
        CombineStacks = new CombineStack[division];
        for (var index = 0; index < CombineStacks.Length; index++)
        {
            CombineStacks[index] = new CombineStack();
        }
    }

    public void PlaceEvent(int source_measure, int index, string event_name)
    {
        if (string.IsNullOrWhiteSpace(event_name)) return;
        
        var real_division = CombineStacks.Length / source_measure;
        var real_index = index * real_division;

        var stack = CombineStacks[real_index];
        stack.Events.Add(event_name + '|');
    }

    public string Export()
    {
        return string.Concat(CombineStacks.Select(r => r.Export()));
    }
}
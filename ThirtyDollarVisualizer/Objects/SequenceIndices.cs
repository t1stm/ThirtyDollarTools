namespace ThirtyDollarVisualizer.Objects;

public struct SequenceIndices
{
    public (ulong time, int sequence_index)[] Ends = Array.Empty<(ulong, int)>();

    public SequenceIndices()
    {
    }
    
    public int GetIndexFromTime(ulong time)
    {
        var last_index = 0;
        foreach (var (end, _) in Ends)
        {
            if (time >= end)
            {
                last_index++;
            }
            else break;
        }

        return Math.Min(last_index, Ends.Length - 1);
    }
}
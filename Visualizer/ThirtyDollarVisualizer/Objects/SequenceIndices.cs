namespace ThirtyDollarVisualizer.Objects;

public struct SequenceIndices
{
    public (ulong time, int sequence_index)[] Ends = [];

    public SequenceIndices()
    {
    }

    public int GetSequenceIDFromIndex(ulong time)
    {
        var last_index = 0;
        foreach (var (end, _) in Ends)
            if (time >= end)
                last_index++;
            else break;

        return Math.Clamp(last_index, 0, Ends.Length - 1);
    }
}
namespace ThirtyDollarVisualizer.Renderer.Buffers;

public class TrackedBufferReference<TDataType> where TDataType : unmanaged
{
    public required Func<int, TDataType> Lookup { get; init; }
    public required Action<int, TDataType> Update { get; init; }

    public int Index { get; set; }

    public TDataType Value
    {
        get => Lookup(Index);
        set => Update(Index, value);
    }
}
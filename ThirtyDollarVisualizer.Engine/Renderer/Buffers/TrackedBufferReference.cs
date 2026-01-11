namespace ThirtyDollarVisualizer.Engine.Renderer.Buffers;

/// <summary>
/// Represents a reference to an element within a GPU buffer that is tracked for
/// changes, ensuring efficient synchronization of modifications back to the GPU buffer.
/// </summary>
/// <typeparam name="TDataType">
/// The type of the element being referenced. Must be an unmanaged type to ensure compatibility
/// with low-level GPU memory operations.
/// </typeparam>
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
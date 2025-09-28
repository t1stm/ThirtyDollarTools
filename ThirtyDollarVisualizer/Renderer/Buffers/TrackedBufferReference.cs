namespace ThirtyDollarVisualizer.Renderer;

public class TrackedBufferReference<TDataType> where TDataType : unmanaged
{
    private TDataType _value;
    public required Action<int, TDataType> OnChange { get; init; }

    public int Index { get; set; }

    public TDataType Value
    {
        get => _value;
        set
        {
            _value = value;
            OnChange(Index, _value);
        }
    }
}
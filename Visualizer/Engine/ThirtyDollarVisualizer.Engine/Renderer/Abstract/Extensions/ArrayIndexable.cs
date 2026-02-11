namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract.Extensions;

public readonly ref struct ArrayIndexable<T>(Span<T> span) : IIndexableCollection<int, T>
{
    private readonly Span<T> _span = span;

    public T this[int key]
    {
        get => _span[key];
        set => _span[key] = value;
    }

    public int Count => _span.Length;

    public static implicit operator ArrayIndexable<T>(T[] array)
    {
        return new ArrayIndexable<T>(array.AsSpan());
    }

    public static implicit operator ArrayIndexable<T>(Span<T> array)
    {
        return new ArrayIndexable<T>(array);
    }
}
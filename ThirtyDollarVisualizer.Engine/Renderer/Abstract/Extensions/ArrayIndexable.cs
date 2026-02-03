namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract.Extensions;

public readonly ref struct ArrayIndexable<T>(T[] array) : IIndexableCollection<int, T>
{
    public T this[int key]
    {
        get => array[key];
        set => array[key] = value;
    }

    public int Count => array.Length;

    public static implicit operator ArrayIndexable<T>(T[] array) => new(array);
}
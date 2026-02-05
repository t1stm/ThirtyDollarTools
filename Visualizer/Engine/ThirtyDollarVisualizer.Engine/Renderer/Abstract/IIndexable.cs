namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract;

public interface IIndexable<in TKey, TValue>
{
    TValue this[TKey key] { get; set; }
}
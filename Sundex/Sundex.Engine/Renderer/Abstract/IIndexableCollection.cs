namespace Sundex.Engine.Renderer.Abstract;

public interface IIndexableCollection<in TKey, TValue> : IIndexable<TKey, TValue>
{
    public int Count { get; }
}
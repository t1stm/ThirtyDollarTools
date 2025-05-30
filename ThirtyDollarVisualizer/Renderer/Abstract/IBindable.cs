namespace ThirtyDollarVisualizer.Renderer.Abstract;

public interface IBindable
{
    public int Handle { get; }
    public void Bind();
}
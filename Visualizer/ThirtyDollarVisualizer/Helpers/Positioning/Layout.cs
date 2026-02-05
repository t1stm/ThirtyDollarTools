using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;

namespace ThirtyDollarVisualizer.Helpers.Positioning;

public struct Resizable
{
    public IPositionable Renderable;
    public Action<float, float>? OnResize;
}

public class Layout(float width, float height)
{
    protected readonly Dictionary<string, Resizable> Resizables = new();
    protected Vector2 Size = (width, height);

    public T Get<T>(ReadOnlySpan<char> field) where T : IPositionable
    {
        var alternative = Resizables.GetAlternateLookup<ReadOnlySpan<char>>();

        return !alternative.TryGetValue(field, out var resizable)
            ? throw new Exception($"No renderable found for field {field}")
            : (resizable.Renderable is T renderable ? renderable : default) ??
              throw new Exception($"Unable to cast renderable: {field}, to type {typeof(T)}");
    }

    public T Add<T>(string field,
        Func<T> factory,
        Action<T>? onCreated = null,
        Action<T, float, float>? onResize = null) where T : IPositionable
    {
        var new_instance = factory();
        var resizable = new Resizable
        {
            Renderable = new_instance,
            OnResize = (width, height) => onResize?.Invoke(new_instance, width, height)
        };

        Resizables.Add(field, resizable);
        onCreated?.Invoke(new_instance);

        onResize?.Invoke(new_instance, Size.X, Size.Y);
        return new_instance;
    }

    public void Resize(float width, float height)
    {
        Size = (width, height);
        foreach (var (_, resizable) in Resizables) resizable.OnResize?.Invoke(width, height);
    }
}
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Base_Objects;

namespace ThirtyDollarVisualizer.Helpers.Positioning;

public struct Resizable
{
    public Renderable Renderable;
    public Action<float, float>? OnResize;
}

public class Layout(float width, float height)
{
    protected readonly Dictionary<string, Resizable> Resizables = new();
    protected Vector2 Size = (width, height);

    public T Get<T>(ReadOnlySpan<char> field) where T : Renderable
    {
        var alternative = Resizables.GetAlternateLookup<ReadOnlySpan<char>>();

        return !alternative.TryGetValue(field, out var resizable)
            ? throw new Exception($"No renderable found for field {field}")
            : resizable.Renderable.As<T>() ??
              throw new Exception($"Unable to cast renderable: {field}, to type {typeof(T)}");
    }

    public T Add<T>(string field, Action<T> onCreate,
        Action<T, float, float>? onResize = null) where T : Renderable, new()
    {
        var new_instance = new T();
        var resizable = new Resizable
        {
            Renderable = new_instance,
            OnResize = (width, height) => onResize?.Invoke(new_instance, width, height)
        };

        Resizables.Add(field, resizable);
        onCreate.Invoke(new_instance);

        onResize?.Invoke(new_instance, Size.X, Size.Y);
        return new_instance;
    }

    public void Resize(float width, float height)
    {
        Size = (width, height);
        foreach (var (_, resizable) in Resizables) resizable.OnResize?.Invoke(width, height);
    }

    public void Render(Camera camera)
    {
        // fucking resharper
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var resizable in Resizables.Values)
        {
            var renderable = resizable.Renderable;
            renderable.Render(camera);
        }
    }
}
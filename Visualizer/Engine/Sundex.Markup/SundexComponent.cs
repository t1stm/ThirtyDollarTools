using Sundex.Components.Abstractions;
using Sundex.Markup.Abstract;

namespace Sundex.Markup;

public class SundexComponent : ISundexComponent
{
    public required string Version { get; set; }
    public Action<object?>? RunLogic { get; set; }

    public Dictionary<string, UIElement> RegisteredIDs { get; init; } = [];
    public Dictionary<string, List<UIElement>> RegisteredClasses { get; init; } = [];
    public required ISundexContext Context { get; init; }
    public required UIElement Element { get; set; }

    public HashSet<ISundexComponent> Dependencies { get; init; } = [];
    public List<ISundexComponent> Children { get; init; } = [];

    public T GetID<T>(string id) where T : UIElement
    {
        if (!RegisteredIDs.TryGetValue(id, out var element))
            throw new Exception($"Unable to find element with id: {id}");
        return element as T ?? throw new Exception($"Element with id: {id} is not of type {typeof(T)}");
    }

    public string? Name { get; init; }
}
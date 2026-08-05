using Sundex.Components.Abstractions;
using Sundex.Markup.Abstract;
using Sundex.Style.DSL;

namespace Sundex.Markup;

public class SundexComponent : ISundexComponent
{
    public required string Version { get; set; }
    public Action<object?>? RunLogic { get; set; }

    /// <summary>
    ///     The sheet this component was built with, or null when the document declared no
    ///     style. Exposed so code-built UI can style itself from the same sheet, and so
    ///     values that aren't element properties (colors used in raw draw calls) can be
    ///     read out of it instead of being compiled in.
    /// </summary>
    public StyleSheet? StyleSheet { get; init; }

    public Dictionary<string, UIElement> RegisteredIDs { get; init; } = [];
    public Dictionary<string, List<UIElement>> RegisteredClasses { get; init; } = [];
    public required ISundexContext Context { get; init; }
    public required UIElement Element { get; set; }

    public HashSet<ISundexComponent> Dependencies { get; init; } = [];
    public List<ISundexComponent> Children { get; init; } = [];

    public string? Name { get; init; }

    public T GetID<T>(string id) where T : UIElement
    {
        if (!RegisteredIDs.TryGetValue(id, out var element))
            throw new Exception($"Unable to find element with id: {id}");
        return element as T ?? throw new Exception($"Element with id: {id} is not of type {typeof(T)}");
    }
}
using Sundex.Components.Abstractions;
using Sundex.Markup.Abstract;

namespace Sundex.Markup;

public class SundexComponent : ISundexComponent
{
    public required string Version { get; set; }
    public Action<object?>? RunLogic { get; init; }

    public Dictionary<string, UIElement> RegisteredIDs { get; init; } = [];
    public Dictionary<string, List<UIElement>> RegisteredClasses { get; init; } = [];
    public required ISundexContext Context { get; init; }
    public required UIElement Element { get; set; }

    public HashSet<ISundexComponent> Dependencies { get; init; } = [];
    public List<ISundexComponent> Children { get; init; } = [];

    public string? Name { get; init; }
}
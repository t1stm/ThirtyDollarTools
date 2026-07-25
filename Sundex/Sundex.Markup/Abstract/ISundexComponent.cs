using Sundex.Components.Abstractions;

namespace Sundex.Markup.Abstract;

public interface ISundexComponent
{
    public string? Name { get; init; }
    public ISundexContext Context { get; }
    public UIElement Element { get; set; }

    public HashSet<ISundexComponent> Dependencies { get; init; }
    public List<ISundexComponent> Children { get; init; }
}
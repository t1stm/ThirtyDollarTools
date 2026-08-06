using Sundex.Components.Abstractions;
using Sundex.Markup.Document;

namespace Sundex.Markup.Abstract;

public interface ISundexComponent
{
    public string? Name { get; init; }
    public ISundexContext Context { get; }
    public UIElement Element { get; set; }

    /// <summary>
    ///     The document this component was built from. Retained so a registered component
    ///     used as a tag elsewhere can be rebuilt per usage site instead of aliasing its
    ///     element tree - <see cref="Document.Layout.LayoutContainer.BuildTree" /> reparses
    ///     from the held XmlElement, so building the same document twice is independent.
    /// </summary>
    public SundexDocument Document { get; init; }

    public HashSet<ISundexComponent> Dependencies { get; init; }
    public List<ISundexComponent> Children { get; init; }
}
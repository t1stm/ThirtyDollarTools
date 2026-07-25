using Sundex.Components.Abstractions;

namespace Sundex.Markup.Abstract;

public interface ISundexContext
{
    UIContext UIContext { get; }
    ISundexComponent ResolveComponent(ReadOnlySpan<char> dependency);
    void RegisterComponent(ISundexComponent component);
    void RegisterElementFactory(string tagName, Func<UIContext, UIElement> factory);
    UIElement? CreateElement(string tagName);
}
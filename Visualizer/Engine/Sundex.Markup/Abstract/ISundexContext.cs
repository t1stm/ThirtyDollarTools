using Sundex.Components.Abstractions;

namespace Sundex.Markup.Abstract;

public interface ISundexContext
{
    UIContext UIContext { get; }
    ISundexComponent ResolveComponent(ReadOnlySpan<char> dependency);
    void RegisterComponent(ISundexComponent component);
}
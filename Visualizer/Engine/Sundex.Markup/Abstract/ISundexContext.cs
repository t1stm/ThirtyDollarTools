using Sundex.Components.Abstractions;

namespace Sunder.Markup.Abstract;

public interface ISundexContext
{
    UIContext UIContext { get; }
    ISundexComponent ResolveComponent(ReadOnlySpan<char> dependency);
    void RegisterComponent(ISundexComponent component);
}
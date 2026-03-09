using Sunder.Markup.Abstract;
using Sunder.Markup.Builders;
using Sundex.Components.Abstractions;

namespace Sunder.Markup;

public class SundexContext<T>(T contextProvider, UIContext context) : ISundexContext where T : class
{
    public T ContextProvider { get; } = contextProvider;

    public Dictionary<string, IComponentBuilder> ComponentBuilderVersions { get; } = new()
    {
        { ComponentBuilderV1.Version, new ComponentBuilderV1() }
    };

    public Dictionary<string, ISundexComponent> LoadedComponents { get; } = [];
    public UIContext UIContext { get; } = context;

    public ISundexComponent ResolveComponent(ReadOnlySpan<char> dependency)
    {
        var lookup = LoadedComponents.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(dependency, out var component)
            ? component
            : throw new Exception($"Unable to find component: {dependency}");
    }

    public void RegisterComponent(ISundexComponent component)
    {
        if (component.Name == null) throw new Exception("Component name cannot be null.");
        LoadedComponents.Add(component.Name, component);
    }

    public SundexComponent NewComponent(string smxlMarkup)
    {
        var layout = MarkupParser.Parse(smxlMarkup);
        var version = layout.Root.Version;

        var lookup = ComponentBuilderVersions.GetAlternateLookup<ReadOnlySpan<char>>();
        return !lookup.TryGetValue(version, out var builder)
            ? throw new Exception("No builder found for the version specified in the document markup.")
            : builder.CreateComponent(layout, this);
    }

    public void RegisterBuilder(string version, IComponentBuilder builder)
    {
        if (!ComponentBuilderVersions.TryAdd(version, builder))
            throw new Exception("A builder with the same version already exists.");
    }
}
using System.Buffers;
using System.Reflection;
using Sundex.Components.Abstractions;
using Sundex.Markup.Abstract;
using Sundex.Markup.Attributes;
using Sundex.Markup.Builders;

namespace Sundex.Markup;

public class SundexContext(UIContext context) : ISundexContext
{
    public Dictionary<string, IComponentBuilder> ComponentBuilderVersions { get; } = new()
    {
        { ComponentBuilderV1.Version, new ComponentBuilderV1() }
    };

    public Dictionary<string, ISundexComponent> LoadedComponents { get; } = [];
    public Dictionary<string, Func<UIContext, UIElement>> ElementFactories { get; } = [];
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

    public void UnregisterComponent(ISundexComponent component)
    {
        if (component.Name == null) throw new Exception("Component name cannot be null.");
        LoadedComponents.Remove(component.Name);
    }
    
    public void RegisterElementFactory(string tagName, Func<UIContext, UIElement> factory)
    {
        if (!ElementFactories.TryAdd(tagName, factory))
            throw new Exception($"A factory for tag '{tagName}' is already registered.");
    }

    public UIElement? CreateElement(string tagName)
    {
        var lookup = ElementFactories.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(tagName, out var factory) ? factory(UIContext) : null;
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

    public void RunLogicAndVerify(SundexComponent component, params ReadOnlySpan<Func<object?>> objectGetters)
    {
        var previousValuesRent = ArrayPool<object?>.Shared.Rent(objectGetters.Length);
        try
        {
            var previousValues = previousValuesRent.AsSpan()[..objectGetters.Length];
            object? target = null;
            for (var index = 0; index < objectGetters.Length; index++)
            {
                var obj = objectGetters[index].Invoke();
                previousValues[index] = obj;
                if (target == null && objectGetters[index].Target != null)
                    target = objectGetters[index].Target;
            }

            component.RunLogic?.Invoke(target ?? this);

            for (var i = 0; i < objectGetters.Length; i++)
            {
                var oldObj = previousValues[i];
                var newObj = objectGetters[i].Invoke();
                if (newObj is null && oldObj is not null)
                    throw new Exception(
                        $"Object getter {objectGetters[i]} returned null but was expecting a non-null value.");
            }

            if (target == null) return;
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetCustomAttribute<SetFromLogicAttribute>() == null) continue;
                if (property.GetValue(target) == null)
                    throw new Exception(
                        $"Property {property.Name} in {type.Name} was marked with [SetFromLogic] but remains null after logic execution.");
            }

            foreach (var field in type.GetFields(flags))
            {
                if (field.GetCustomAttribute<SetFromLogicAttribute>() == null) continue;
                if (field.GetValue(target) == null)
                    throw new Exception(
                        $"Field {field.Name} in {type.Name} was marked with [SetFromLogic] but remains null after logic execution.");
            }

            const BindingFlags nonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var property in type.GetProperties(nonPublicFlags))
                if (property.GetCustomAttribute<SetFromLogicAttribute>() != null)
                    throw new Exception(
                        $"Property {property.Name} in {type.Name} is marked with [SetFromLogic] but is not public.");

            foreach (var field in type.GetFields(nonPublicFlags))
                if (field.GetCustomAttribute<SetFromLogicAttribute>() != null)
                    throw new Exception(
                        $"Field {field.Name} in {type.Name} is marked with [SetFromLogic] but is not public.");
        }
        finally
        {
            ArrayPool<object?>.Shared.Return(previousValuesRent);
        }
    }
}
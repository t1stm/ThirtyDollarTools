using Sunder.Markup.Document;
using Sunder.Markup.Logic;
using Sunder.Markup.State;
using Sunder.Markup.Style;

namespace Sunder.Markup;

public class SundexContext
{
    // base => [flex, stack, label, etc...]
    // controls => [button, input (future), dropdown (future)]
    public Dictionary<string, List<SundexComponent>> ComponentCollections { get; } = []; 
    
    public SundexComponent LoadComponent(SundexDocument document)
    {
        var lookup = ComponentCollections.GetAlternateLookup<ReadOnlySpan<char>>();

        var componentName = document.Root.Component;
        var imports = document.Root.Imports;
        HashSet<SundexComponent> dependencies = [];

        LoadDependenciesFromCollections(imports, lookup, dependencies);
        var state = new SundexState();
        
        SundexLogic? logic = null;
        if (document.Logic is not null)
        {
            logic = new SundexLogic(this, document.Logic, state);
        }
        
        SundexStyle? style = null;
        if (document.Style is not null)
        {
            style = new SundexStyle(this, document.Style, state);
        }
        
        var component = new SundexComponent(this, state)
        {
            Name = componentName,
            Dependencies = dependencies,
            Logic = logic,
            Style = style
        };
        
        // stage: finalize add component to collections
        AddComponentToLookup(document, componentName, lookup, component);
        return component;
    }

    private static void AddComponentToLookup(SundexDocument document, string? componentName, Dictionary<string, List<SundexComponent>>.AlternateLookup<ReadOnlySpan<char>> lookup,
        SundexComponent component)
    {
        if (componentName is not null)
        {
            lookup.TryAdd(componentName, [component]);
        }

        foreach (var collection in document.Root.Collections)
        {
            if (!lookup.TryGetValue(collection, out var existing))
            {
                lookup.TryAdd(collection, [component]);
                continue;
            }
            
            existing.Add(component);
        }
    }

    private void LoadDependenciesFromCollections(List<string> imports, Dictionary<string, List<SundexComponent>>.AlternateLookup<ReadOnlySpan<char>> lookup, HashSet<SundexComponent> dependencies)
    {
        foreach (var import in imports)
        {
            if (lookup.TryGetValue(import, out var collection))
            {
                foreach (var comp in collection)
                    dependencies.Add(comp);
                continue;
            }

            foreach (var (_, components) in ComponentCollections)
            {
                var found = components.Find(comp => comp.Name == import);
                if (found == null) continue;

                dependencies.Add(found);
                break;
            }
        }
    }
}
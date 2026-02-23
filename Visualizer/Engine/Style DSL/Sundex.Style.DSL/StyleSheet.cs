using Sundex.Style.DSL.Abstract;

namespace Sundex.Style.DSL;

public class StyleSheet
{
    public Dictionary<string, Dictionary<string, IStyleValue>> Animations { get; } = new();
    public Dictionary<string, Dictionary<string, IStyleValue>> Components { get; } = new();
    public Dictionary<string, Dictionary<string, IStyleValue>> Classes { get; } = new();
    public Dictionary<string, Dictionary<string, IStyleValue>> IDTags { get; } = new();
    public HashSet<string> FullOverrides { get; } = [];

    public IStyleValue? GetStyleValueForTag(string tagName, string property)
    {
        var ids = IDTags.GetAlternateLookup<ReadOnlySpan<char>>();
        var classes = Classes.GetAlternateLookup<ReadOnlySpan<char>>();
        var components = Components.GetAlternateLookup<ReadOnlySpan<char>>();

        if (ids.TryGetValue(tagName, out var idProps) && idProps.TryGetValue(property, out var idValue)) return idValue;
        if (classes.TryGetValue(tagName, out var classProps) && classProps.TryGetValue(property, out var classValue))
            return classValue;
        if (components.TryGetValue(tagName, out var componentProps) &&
            componentProps.TryGetValue(property, out var componentValue)) return componentValue;
        return null;
    }

    public void Merge(StyleSheet other)
    {
        MergeDictionary(Animations, other.Animations);
        MergeDictionary(Components, other.Components);
        MergeDictionary(Classes, other.Classes);
        MergeDictionary(IDTags, other.IDTags);
        foreach (var name in other.FullOverrides)
        {
            FullOverrides.Add(name);
        }
    }

    private static void MergeDictionary(Dictionary<string, Dictionary<string, IStyleValue>> target,
        Dictionary<string, Dictionary<string, IStyleValue>> source)
    {
        foreach (var kvp in source)
        {
            if (target.TryGetValue(kvp.Key, out var existingProps))
            {
                foreach (var prop in kvp.Value)
                {
                    existingProps[prop.Key] = prop.Value;
                }
            }
            else
            {
                target[kvp.Key] = new Dictionary<string, IStyleValue>(kvp.Value);
            }
        }
    }
}
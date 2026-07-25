using Sundex.Style.DSL.Abstract;

namespace Sundex.Style.DSL;

public class StyleSheetHolder
{
    public Dictionary<string, Dictionary<string, IStyleValue>> Animations { get; } = new();
    public Dictionary<string, Dictionary<string, IStyleValue>> Components { get; } = new();
    public Dictionary<string, Dictionary<string, IStyleValue>> Classes { get; } = new();
    public Dictionary<string, Dictionary<string, IStyleValue>> IDTags { get; } = new();
    public HashSet<string> FullOverrides { get; } = [];

    public void Merge(StyleSheetHolder other)
    {
        MergeDictionary(Animations, other.Animations);
        MergeDictionary(Components, other.Components);
        MergeDictionary(Classes, other.Classes);
        MergeDictionary(IDTags, other.IDTags);
        foreach (var name in other.FullOverrides) FullOverrides.Add(name);
    }

    private static void MergeDictionary(Dictionary<string, Dictionary<string, IStyleValue>> target,
        Dictionary<string, Dictionary<string, IStyleValue>> source)
    {
        foreach (var kvp in source)
            if (target.TryGetValue(kvp.Key, out var existingProps))
                foreach (var prop in kvp.Value)
                    existingProps[prop.Key] = prop.Value;
            else
                target[kvp.Key] = new Dictionary<string, IStyleValue>(kvp.Value);
    }
}
using Sundex.Style.DSL.Abstract;

namespace Sundex.Style.DSL;

public class StyleSheetHolder
{
    public Dictionary<string, Dictionary<string, IStyleValue>> Animations { get; } = new();
    public Dictionary<string, Dictionary<string, IStyleValue>> Components { get; } = new();
    public Dictionary<string, Dictionary<string, IStyleValue>> Classes { get; } = new();
    public Dictionary<string, Dictionary<string, IStyleValue>> IDTags { get; } = new();
    public HashSet<string> FullOverrides { get; } = [];

    /// <summary>
    ///     Constants declared with <c>var</c>, plus the ones merged in from unaliased imports.
    /// </summary>
    public Dictionary<string, IStyleValue> Variables { get; } = new();

    /// <summary>
    ///     Variables of aliased imports (<c>import "..." as name;</c>), keyed by alias.
    ///     Aliases belong to the file that declared them and are never merged.
    /// </summary>
    public Dictionary<string, Dictionary<string, IStyleValue>> Namespaces { get; } = new();

    public void Merge(StyleSheetHolder other, bool includeVariables = true)
    {
        MergeDictionary(Animations, other.Animations);
        MergeDictionary(Components, other.Components);
        MergeDictionary(Classes, other.Classes);
        MergeDictionary(IDTags, other.IDTags);
        foreach (var name in other.FullOverrides) FullOverrides.Add(name);
        if (includeVariables)
            foreach (var (name, value) in other.Variables)
                Variables[name] = value;
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
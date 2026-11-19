using System.Reflection;
using Sundex.Style.DSL;

namespace Sundex.Components.Abstractions;

/// <summary>
///     <c>media[min-width = 640px] = { … }</c> in a rule: applied while the viewport matches, and
///     re-styled when it stops or starts matching. Only elements whose rules have such a block are
///     watched for it.
/// </summary>
public abstract partial class UIElement
{
    private long _mediaKey;
    private bool _usesMedia;

    /// <summary>Which of this element's media blocks hold now, one bit each (the first 64).</summary>
    internal long MediaKey()
    {
        if (StoredStyleSheet is not { } sheet) return 0;
        var width = Context.ViewportWidth;
        var height = Context.ViewportHeight;
        long key = 0;
        var bit = 0;

        void Add(string name)
        {
            foreach (var rule in sheet.GetMediaRulesForTag(name))
            {
                if (rule.Condition.Holds(width, height) && bit < 64) key |= 1L << bit;
                bit++;
            }
        }

        Add(Tag);
        foreach (var cls in Classes) Add(cls);
        Add(ID);
        return key;
    }

    /// <summary>Marks the element as watching the viewport when the sheet gives its tag, a class or its id a media block.</summary>
    private void WatchesMedia(StyleSheet sheet)
    {
        var any = sheet.GetMediaRulesForTag(Tag).Length > 0 || sheet.GetMediaRulesForTag(ID).Length > 0;
        foreach (var cls in Classes) any |= sheet.GetMediaRulesForTag(cls).Length > 0;
        if (!any) return;
        _usesMedia = true;
        _mediaKey = MediaKey();
    }

    /// <summary>Whether a media block of <paramref name="name" /> that holds now sets <paramref name="setting" />.</summary>
    private bool MediaSets(StyleSheet sheet, string name, string setting)
    {
        foreach (var rule in sheet.GetMediaRulesForTag(name))
            if (rule.Properties.ContainsKey(setting) && rule.Condition.Holds(Context.ViewportWidth, Context.ViewportHeight))
                return true;
        return false;
    }

    /// <summary>Re-styles if the viewport changed which media blocks hold. Called by the context on a resize.</summary>
    internal void RefreshMedia()
    {
        if (MediaKey() != _mediaKey) Restyle();
    }

    private bool ApplyMedia(StyleSheet sheet, string name, PropertyInfo property, string setting)
    {
        var rules = sheet.GetMediaRulesForTag(name);
        if (rules.Length == 0) return false;
        _usesMedia = true;
        _mediaKey = MediaKey();

        var matched = false;
        foreach (var rule in rules)
        {
            if (!rule.Condition.Holds(Context.ViewportWidth, Context.ViewportHeight)) continue;
            if (!rule.Properties.TryGetValue(setting, out var value)) continue;
            ApplyStyleValue(sheet, value, property);
            matched = true;
        }

        return matched;
    }
}

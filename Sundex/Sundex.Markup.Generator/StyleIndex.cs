using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Sundex.Markup.Generator;

/// <summary>The class, component and id names a <c>.snx.ss</c> file declares, and what it imports.</summary>
internal sealed class StyleIndex(string path)
{
    private static readonly Regex Block = new(@"^\s*@?(class|component|id)\s+([\w-]+)", RegexOptions.Multiline);
    private static readonly Regex Import = new(@"^\s*import\s+""([^""]+)""", RegexOptions.Multiline);

    // A state block inside a media block: the DSL can't read it, and says so only when the sheet loads.
    private static readonly Regex StateInMedia = new(@"media\[[^\]]*\]\s*=\s*\{[^}]*state\[");

    public string Path { get; } = path.Replace('\\', '/');
    public HashSet<string> Names { get; } = new(StringComparer.Ordinal);
    public List<string> Imports { get; } = [];

    /// <summary>Lines the DSL would refuse at runtime, found at build time instead.</summary>
    public List<int> StateInMediaLines { get; } = [];

    public static StyleIndex Parse(string path, string text)
    {
        var index = new StyleIndex(path);
        foreach (Match match in Block.Matches(text)) index.Names.Add(match.Groups[2].Value);
        foreach (Match match in Import.Matches(text)) index.Imports.Add(match.Groups[1].Value);
        var lines = text.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            if (StateInMedia.IsMatch(lines[lineIndex]))
                index.StateInMediaLines.Add(lineIndex + 1);
        return index;
    }

    /// <summary>
    ///     Every name the sheet at <paramref name="source" /> (an asset location) and its imports declare,
    ///     or null when it isn't among the project's files, which makes the check impossible, not failed.
    /// </summary>
    public static HashSet<string>? NamesFor(string? source, ImmutableArray<StyleIndex> sheets)
    {
        if (source == null) return null;
        var names = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return Collect(source, sheets, names, seen) ? names : null;
    }

    /// <summary>Whether the file at <paramref name="path" /> is the one an asset <paramref name="location" /> names.</summary>
    public static bool IsAt(string path, string location)
    {
        path = path.Replace('\\', '/');
        var wanted = location.Replace('\\', '/').TrimStart('/');
        return path.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase) || path == wanted;
    }

    private static bool Collect(string location, ImmutableArray<StyleIndex> sheets, HashSet<string> names,
        HashSet<string> seen)
    {
        StyleIndex? sheet = null;
        foreach (var candidate in sheets)
            if (IsAt(candidate.Path, location))
                sheet = candidate;
        if (sheet == null) return false;
        if (!seen.Add(sheet.Path)) return true;

        names.UnionWith(sheet.Names);
        foreach (var import in sheet.Imports)
            if (!Collect(import, sheets, names, seen))
                return false;
        return true;
    }
}

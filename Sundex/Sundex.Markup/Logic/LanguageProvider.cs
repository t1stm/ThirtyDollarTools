using System.Collections.Concurrent;
using Sundex.Markup.Logic.Languages;

namespace Sundex.Markup.Logic;

public static class LanguageProvider
{
    private static readonly ConcurrentDictionary<string, SundexScript> Registered = new(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, SundexScript> Languages => Registered;

    public static void Register(string name, SundexScript script) => Registered[name] = script;
}

using Sundex.Markup.Logic.Languages;
using Sundex.Markup.Logic.Languages.CSharp;

namespace Sundex.Markup.Logic;

public static class LanguageProvider
{
    public static Dictionary<string, SundexScript> Languages { get; } = new()
    {
        { "csharp", new CSharp() }
    };
}
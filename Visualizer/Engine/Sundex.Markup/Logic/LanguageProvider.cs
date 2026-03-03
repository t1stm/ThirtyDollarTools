using Sunder.Markup.Logic.Languages;
using Sunder.Markup.Logic.Languages.CSharp;

namespace Sunder.Markup.Logic;

public static class LanguageProvider
{
    public static Dictionary<string, SundexScript> Languages { get; } = new()
    {
        {"csharp", new CSharpScript()}
    };
}
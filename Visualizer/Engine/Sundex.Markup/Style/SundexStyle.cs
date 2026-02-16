using Sunder.Markup.Document.Style;
using Sunder.Markup.State;
using Sunder.Markup.Style.Languages;

namespace Sunder.Markup.Style;

public class SundexStyle
{
    public static Dictionary<string, SundexStyleLanguage> Languages { get; } = new()
    {
        {
            "engine", new EngineStyleLanguage()
        }
    };

    public SundexStyle(SundexContext sundexContext, StyleContainer documentStyle, SundexState sundexState)
    {
        Context = sundexContext;
        State = sundexState;

        var language = documentStyle.Language;
        var style = documentStyle.StyleString;
    }

    public SundexContext Context { get; }
    public SundexState State { get; }
}
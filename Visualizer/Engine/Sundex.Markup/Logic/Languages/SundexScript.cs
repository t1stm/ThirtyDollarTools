using Sundex.Markup.Abstract;

namespace Sundex.Markup.Logic.Languages;

public abstract class SundexScript
{
    /// <summary>
    ///     Compiles the provided Sundex script source code into an executable action.
    /// </summary>
    /// <param name="sourceCode">The source code to compile.</param>
    /// <param name="context">The Sundex context for the script execution.</param>
    /// <param name="logicLanguageImports"></param>
    /// <returns>An executable action representing the compiled script.</returns>
    public abstract Action<object?> Compile(string sourceCode, ISundexContext context,
        List<string> logicLanguageImports);
}
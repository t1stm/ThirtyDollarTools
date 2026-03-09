namespace Sunder.Markup.Logic.Languages;

public abstract class SundexScript
{
    /// <summary>
    ///     Compiles the provided Sundex script source code into an executable action.
    /// </summary>
    /// <param name="sourceCode">The source code to compile.</param>
    /// <param name="context">Optional context for the script execution.</param>
    /// <returns>An executable action representing the compiled script.</returns>
    public abstract Action Compile(string sourceCode, object? context);
}
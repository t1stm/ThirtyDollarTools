using Sundex.Markup.Abstract;

namespace Sundex.Markup.Logic.Languages;

public abstract class SundexScript
{
    /// <summary>
    ///     Compiles the provided Sundex script source code into an executable action.
    /// </summary>
    /// <param name="sourceCode">The source code to compile.</param>
    /// <param name="context">The Sundex context for the script execution.</param>
    /// <param name="component">The component associated with the script.</param>
    /// <param name="logicLanguageImports"></param>
    /// <returns>An executable action representing the compiled script.</returns>
    public abstract Action<object?> Compile(string sourceCode, ISundexContext context,
        SundexComponent component,
        List<string> logicLanguageImports);

    /// <summary>
    ///     Compiles the source and caches the result without binding it to a component, so
    ///     the <see cref="Compile" /> that eventually wants it is handed a finished script.
    ///     Compilation needs no graphics context, which is what makes this worth doing from
    ///     a worker thread ahead of the frame that builds the component.
    ///     <para>
    ///         Doing nothing is a valid implementation: it only costs the caller the compile
    ///         it was trying to avoid.
    ///     </para>
    /// </summary>
    public virtual void Precompile(string sourceCode, ISundexContext context,
        List<string> logicLanguageImports)
    {
    }
}
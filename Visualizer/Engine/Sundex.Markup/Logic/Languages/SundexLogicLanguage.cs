using Sunder.Markup.State;

namespace Sunder.Markup.Logic.Languages;

public abstract class SundexLogicLanguage(SundexState state)
{
    public SundexState State { get; } = state;
    
    public abstract void Compile(string sourceCode);
    public abstract void Execute();
}
using Sunder.Markup.Document.Logic;
using Sunder.Markup.State;

namespace Sunder.Markup.Logic;

public class SundexLogic
{
    public SundexLogic(SundexContext sundexContext, LogicContainer documentLogic, SundexState state)
    {
        Context = sundexContext;
        State = state;
    }
    
    public SundexContext Context { get; }
    public SundexState State { get; }
}
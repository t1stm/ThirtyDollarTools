using Sunder.Markup.Logic;
using Sunder.Markup.State;
using Sunder.Markup.Style;

namespace Sunder.Markup;

public class SundexComponent(SundexContext context, SundexState state)
{
    public SundexContext Context { get; } = context;
    public string? Name { get; init; } = "";
    public HashSet<SundexComponent> Dependencies { get; init; } = [];
    public SundexState State { get; } = state;
    
    public SundexStyle? Style { get; init; }
    public SundexLogic? Logic { get; init; }
}
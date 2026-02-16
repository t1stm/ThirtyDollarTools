using Sunder.Markup.State.Tracking;

namespace Sunder.Markup.State;

public class SundexState
{
    public object? Context { get; set; } = null;
    public Dictionary<string, TrackedState<dynamic>> Style { get; } = [];
}
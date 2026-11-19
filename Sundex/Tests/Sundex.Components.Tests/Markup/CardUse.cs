using Sundex.Reactive;

namespace Sundex.Components.Tests.Markup;

/// <summary>What the test reads back from the document's logic.</summary>
public partial class CardUse
{
    public List<string> Picks { get; private set; } = [];
    public Func<int> CloseCount { get; private set; } = () => 0;
    public State<int> Tab { get; private set; } = null!;
}

using Sundex.Reactive;

namespace Sundex.Components.Tests.Markup;

/// <summary>The context variables the test documents read as <c>Context.X</c>.</summary>
public sealed class TestAppContext
{
    public State<string> Greeting { get; } = new("hello");
    public State<List<string>> Items { get; } = new(["a", "b", "c"]);
}

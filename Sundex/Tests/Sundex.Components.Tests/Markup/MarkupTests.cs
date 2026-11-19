using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Markup;
using Sundex.Reactive;

namespace Sundex.Components.Tests.Markup;

/// <summary>Version 2.0 documents, compiled by the generator at build time and run here.</summary>
public class MarkupTests
{
    private static (SundexContext<TestAppContext> Sundex, TestAppContext App) Context()
    {
        var app = new TestAppContext();
        return (new SundexContext<TestAppContext>(new TestUIContext(), app), app);
    }

    private static IEnumerable<UIElement> All(UIElement root)
    {
        yield return root;
        if (root is not Panel panel) yield break;
        foreach (var child in panel.Children)
        foreach (var descendant in All(child))
            yield return descendant;
    }

    private static IEnumerable<Label> Labels(UIElement root, string cls) =>
        All(root).OfType<Label>().Where(label => label.Classes.Contains(cls) && IsShown(label));

    private static bool IsShown(UIElement element)
    {
        for (var current = element; current != null; current = current.Parent)
            if (!current.Visible)
                return false;
        return true;
    }

    private static string Text(Label label) => label.Value.ToString();

    [Fact]
    public void ADocumentBuildsWithItsIdClassesAttributesAndSheet()
    {
        var (sundex, _) = Context();
        var root = new Counter(sundex).Create();

        Assert.IsType<FlexPanel>(root);
        Assert.Equal("counter", root.ID);
        Assert.Equal(LayoutDirection.Vertical, ((FlexPanel)root).Direction);
        var count = Assert.Single(Labels(root, "count"));
        Assert.Equal("Count: 0", Text(count));
        Assert.Equal(20, count.FontSizePx.Value);
        Assert.Equal("hello", Text(Assert.Single(Labels(root, "greeting"))));
    }

    [Fact]
    public void AnEventChangesLocalStateAndBindingsClassesAndBranchesFollow()
    {
        var (sundex, _) = Context();
        var root = new Counter(sundex).Create();
        var button = All(root).OfType<Button>().Single();
        Assert.Equal("none", Text(Assert.Single(Labels(root, "branch"))));

        button.OnClick!(button);
        Rx.Flush();

        var count = Assert.Single(Labels(root, "count"));
        Assert.Equal("Count: 1", Text(count));
        Assert.Contains("odd", count.Classes);
        Assert.Equal(30, count.FontSizePx.Value);
        Assert.Equal("odd", Text(Assert.Single(Labels(root, "branch"))));

        button.OnClick!(button);
        Rx.Flush();

        Assert.DoesNotContain("odd", count.Classes);
        Assert.Equal("even", Text(Assert.Single(Labels(root, "branch"))));
    }

    [Fact]
    public void ContextVariablesAreSharedAndWatched()
    {
        var (sundex, app) = Context();
        var first = new Counter(sundex).Create();
        var second = new Counter(sundex).Create();

        app.Greeting.Value = "bye";
        Rx.Flush();

        Assert.Equal("bye", Text(Assert.Single(Labels(first, "greeting"))));
        Assert.Equal("bye", Text(Assert.Single(Labels(second, "greeting"))));
    }

    [Fact]
    public void EachKeepsRowsByKeyAndShowsEmptyWhenThereAreNone()
    {
        var (sundex, app) = Context();
        var root = new ItemList(sundex).Create();
        Assert.Equal(["1. a", "2. b", "3. c"], Labels(root, "row-name").Select(Text));
        Assert.Equal(3, Labels(root, "extra").Count());
        var rowA = Labels(root, "row-name").First();

        app.Items.Value = ["d", "a", "c"];
        Rx.Flush();

        Assert.Equal(["1. d", "2. a", "3. c"], Labels(root, "row-name").Select(Text));
        // The row for "a" survived the change: same label, new position.
        Assert.Same(rowA, Labels(root, "row-name").ElementAt(1));
        Assert.Empty(Labels(root, "empty"));

        app.Items.Value = [];
        Rx.Flush();
        Assert.Empty(Labels(root, "row-name"));
        Assert.Equal("Nothing here", Text(Assert.Single(Labels(root, "empty"))));

        app.Items.Value = ["x"];
        Rx.Flush();
        Assert.Empty(Labels(root, "empty"));
        Assert.Equal(["1. x"], Labels(root, "row-name").Select(Text));
    }

    [Fact]
    public void DisposingADocumentEndsItsBindings()
    {
        var (sundex, app) = Context();
        var counter = new Counter(sundex);
        var root = counter.Create();
        var greeting = Assert.Single(Labels(root, "greeting"));

        counter.Dispose();
        app.Greeting.Value = "after";
        Rx.Flush();

        Assert.Equal("hello", Text(greeting));
        Assert.Empty(app.Greeting.Observers);
    }

    [Fact]
    public void AComponentCanBeBuiltOnlyOnce()
    {
        var (sundex, _) = Context();
        var counter = new Counter(sundex);
        counter.Create();
        Assert.Throws<InvalidOperationException>(() => counter.Create());
    }

    [Fact]
    public void NamedSlotsFillTheirPlaces_AndAnEmptyOneIsSkipped()
    {
        var (sundex, _) = Context();
        var root = new CardUse(sundex).Create();
        var cards = All(root).Where(element => element.Classes.Contains("card")).ToList();
        Assert.Equal(2, cards.Count);

        Assert.Single(Labels(cards[0], "body"));
        Assert.Equal(["Save", "Cancel"], Labels(cards[0], "action").Select(Text));
        var actions = Assert.Single(All(cards[0]).Where(element => element.Classes.Contains("actions")));
        Assert.All(Labels(cards[0], "action"), label => Assert.Contains(label, All(actions)));

        Assert.Empty(All(cards[1]).Where(element => element.Classes.Contains("actions")));
    }

    [Fact]
    public void ComponentEventsReachTheUse_WithAndWithoutAnArgument()
    {
        var (sundex, _) = Context();
        var use = new CardUse(sundex);
        var root = use.Create();
        var buttons = All(root).OfType<Button>().ToList();

        var picks = buttons.Where(button => button.Classes.Contains("pick")).ToList();
        var close = buttons.First(button => button.Classes.Contains("close"));
        picks[0].OnClick!(picks[0]);
        close.OnClick!(close);
        // The second card has no listeners: raising its events does nothing.
        picks[1].OnClick!(picks[1]);

        Assert.Equal(["First"], use.Picks);
        Assert.Equal(1, use.CloseCount());
    }

    [Fact]
    public void KeepHidesABranchInsteadOfEndingIt()
    {
        var (sundex, _) = Context();
        var use = new CardUse(sundex);
        var root = use.Create();
        var zero = Assert.Single(Labels(root, "kept"));

        use.Tab.Value = 1;
        Rx.Flush();
        Assert.Equal("one", Text(Assert.Single(Labels(root, "kept"))));
        Assert.False(zero.IsDisposed);

        use.Tab.Value = 0;
        Rx.Flush();
        // The same label as before, shown again rather than rebuilt.
        Assert.Same(zero, Assert.Single(Labels(root, "kept")));
    }
}

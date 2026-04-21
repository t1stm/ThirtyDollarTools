using Sundex.Components.Panels;
using Sundex.Components.Scroll;

namespace Sundex.Components.Tests;

public class ScrollBarTests
{
    [Fact]
    public void TestScrollBarInitialization()
    {
        var context = new TestUIContext();
        var parent = new Panel(context) { Width = 100, Height = 500 };
        var scrollBar = new ScrollBar(context, parent);

        // Ensure parent is added to children so it gets layout updates?
        // Actually ScrollBar is a sibling or child?
        // In ScrollBar constructor: Parent = parent; Children = [ScrollBlock];
        // So scrollBar is NOT a child of parent by default.
        parent.Children = [scrollBar];

        parent.Layout();

        Assert.Equal(parent, scrollBar.Parent);
        Assert.Equal(20, scrollBar.Width.Value);
        Assert.Equal(500, scrollBar.Computed.Height);
        Assert.Equal(80, scrollBar.Computed.AbsoluteX - parent.Computed.AbsoluteX);
    }

    [Fact]
    public void TestScrollBarLayout()
    {
        var context = new TestUIContext();
        var parent = new Panel(context) { Width = 100, Height = 500 };
        var scrollBar = new ScrollBar(context, parent);

        scrollBar.Layout();

        Assert.Equal(0, scrollBar.ScrollBlock.X);
        Assert.Equal(0, scrollBar.ScrollBlock.Y);
        Assert.Equal(20, scrollBar.ScrollBlock.Width);
    }
}
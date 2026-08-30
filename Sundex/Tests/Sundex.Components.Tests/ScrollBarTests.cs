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

        // The ScrollBar constructor sets Parent but does not add itself to parent.Children,
        // so it has to be parented explicitly to take part in layout.
        parent.Children = [scrollBar];

        parent.Layout();

        Assert.Equal(parent, scrollBar.Parent);
        Assert.Equal(8, scrollBar.Width.Value);
        Assert.Equal(500, scrollBar.Computed.Height);
        Assert.Equal(92, scrollBar.Computed.AbsoluteX - parent.Computed.AbsoluteX);
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
        Assert.Equal(8, scrollBar.ScrollBlock.Width);
    }
}
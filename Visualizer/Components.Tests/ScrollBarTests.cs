using Components.Abstractions;
using Components.Panels;
using Components.Scroll;
using Xunit;

namespace Components.Tests;

public class ScrollBarTests
{
    [Fact]
    public void TestScrollBarInitialization()
    {
        var context = new TestUIContext();
        var parent = new Panel(context) { Width = 100, Height = 500 };
        var scrollBar = new ScrollBar(context, parent);

        Assert.Equal(parent, scrollBar.Parent);
        Assert.Equal(20, scrollBar.Width);
        Assert.Equal(500, scrollBar.Height);
        Assert.Equal(80, scrollBar.X); // parent.Width - scrollBar.Width = 100 - 20 = 80
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

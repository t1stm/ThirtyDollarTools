using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class FlexPanelTests
{
    private class TestElement(UIContext context)
        : UIElement(context)
    {
        public override string Tag => "test";
        protected override void DrawSelf(UIContext context) { }
    }

    [Fact]
    public void TestHorizontalLayout_AlignStart()
    {
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 100,
            Height = 100,
            Direction = LayoutDirection.Horizontal,
            HorizontalAlign = Align.Start
        };
        var child1 = new TestElement(context) { Width = 20, Height = 20 };
        var child2 = new TestElement(context) { Width = 30, Height = 30 };
        flex.Children = [child1, child2];

        flex.Layout();

        Assert.Equal(0, child1.X);
        Assert.Equal(0, child1.Y);
        Assert.Equal(20, child2.X);
        Assert.Equal(0, child2.Y);
    }

    [Fact]
    public void TestHorizontalLayout_AlignCenter()
    {
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 100,
            Height = 100,
            Direction = LayoutDirection.Horizontal,
            HorizontalAlign = Align.Center
        };
        var child1 = new TestElement(context) { Width = 20, Height = 20 };
        var child2 = new TestElement(context) { Width = 30, Height = 30 };
        flex.Children = [child1, child2];

        flex.Layout();

        // Total width = 20 + 30 = 50.
        // Free space = 100 - 50 = 50.
        // Offset = 50 / 2 = 25.
        // child1.X = 25.
        // offset becomes 25 + 20 + 0 = 45.
        // child2.X = 45.
        Assert.Equal(25, child1.X);
        Assert.Equal(45, child2.X);
    }

    [Fact]
    public void TestVerticalLayout_AlignCenter()
    {
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 100,
            Height = 100,
            Direction = LayoutDirection.Vertical,
            VerticalAlign = Align.Center,
            HorizontalAlign = Align.Center
        };
        var child = new TestElement(context) { Width = 40, Height = 20 };
        flex.Children = [child];

        flex.Layout();

        // Total height = 20. Free = 100 - 20 = 80. Offset = 40.
        // child.Y = 40.
        // HorizontalAlign = Center. innerWidth = 100. child.Width = 40. (100 - 40) / 2 = 30.
        // child.X = 30.
        Assert.Equal(30, child.X);
        Assert.Equal(40, child.Y);
    }

    [Fact]
    public void TestSpacingAndPadding()
    {
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 100,
            Height = 100,
            Direction = LayoutDirection.Horizontal,
            Padding = 10,
            Spacing = 5
        };
        var child1 = new TestElement(context) { Width = 20, Height = 20 };
        var child2 = new TestElement(context) { Width = 20, Height = 20 };
        flex.Children = [child1, child2];

        flex.Layout();

        // Padding = 10.
        // child1.AbsoluteX = parent.AbsoluteX + Padding + child1.X = 0 + 10 + 0 = 10.
        // But FlexPanelTests.TestSpacingAndPadding checks child1.X.
        // child1.X is relative to content origin (after padding).
        // So child1.X should be 0.
        // The test seems to expect child1.X to include padding?
        // Let's check child1.AbsoluteX - flex.AbsoluteX.

        Assert.Equal(10, child1.Computed.AbsoluteX - flex.Computed.AbsoluteX);
        Assert.Equal(35, child2.Computed.AbsoluteX - flex.Computed.AbsoluteX);
    }

    [Fact]
    public void TestAutoWidth()
    {
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 100,
            Height = 100,
            Direction = LayoutDirection.Horizontal
        };
        var child1 = new TestElement(context) { Width = new LiteralOrComputable(100, true), Height = 20 };
        var child2 = new TestElement(context) { Width = 30, Height = 20 };
        flex.Children = [child1, child2];

        flex.Layout();

        // innerWidth = 100. total_fixed = 30. total_spacing = 0.
        // free_space = 70. flex_size = 70 / 1 = 70.
        Assert.Equal(70, child1.Width);
        Assert.Equal(30, child2.Width);
    }
}

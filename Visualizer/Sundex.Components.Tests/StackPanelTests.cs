using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class StackPanelTests
{
    private class TestElement(UIContext context, float width = 0, float height = 0)
        : UIElement(context)
    {
        public override string Tag => "test";
        protected override void DrawSelf(UIContext context) { }
    }

    [Fact]
    public void TestVerticalStack()
    {
        var context = new TestUIContext();
        var stack = new StackPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = 100,
            Height = 200
        };

        var child1 = new TestElement(context, 50, 30);
        var child2 = new TestElement(context, 50, 40);
        stack.Children = [child1, child2];

        stack.Layout();

        Assert.Equal(0, child1.X);
        Assert.Equal(0, child1.Y);
        Assert.Equal(0, child2.X);
        Assert.Equal(30, child2.Y);
    }

    [Fact]
    public void TestHorizontalStack()
    {
        var context = new TestUIContext();
        var stack = new StackPanel(context)
        {
            Direction = LayoutDirection.Horizontal,
            Width = 200,
            Height = 100
        };

        var child1 = new TestElement(context, 50, 30);
        var child2 = new TestElement(context, 60, 30);
        stack.Children = [child1, child2];

        stack.Layout();

        Assert.Equal(0, child1.X);
        Assert.Equal(0, child1.Y);
        Assert.Equal(50, child2.X);
        Assert.Equal(0, child2.Y);
    }

    [Fact]
    public void TestPaddingAndSpacing()
    {
        var context = new TestUIContext();
        var stack = new StackPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Padding = 10,
            Spacing = 5,
            Width = 100,
            Height = 200
        };

        var child1 = new TestElement(context, 50, 30);
        var child2 = new TestElement(context, 50, 40);
        stack.Children = [child1, child2];

        stack.Layout();

        Assert.Equal(10, child1.Computed.X + stack.Computed.AbsoluteX - stack.Computed.AbsoluteX); // child.X is relative to parent
        // Wait, StackPanel.cs:
        // child.X = start_x - Computed.AbsoluteX;
        // start_x = Computed.AbsoluteX + Padding;
        // So child.X should be Padding.

        Assert.Equal(10, child1.X);
        Assert.Equal(10, child1.Y);
        Assert.Equal(10, child2.X);
        Assert.Equal(10 + 30 + 5, child2.Y); // Padding + child1.Height + Spacing
    }

    [Fact]
    public void TestAutoWidthInVerticalStack()
    {
        var context = new TestUIContext();
        var stack = new StackPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Padding = 10,
            Width = 100,
            Height = 200
        };

        var child = new TestElement(context, 0, 30)
        {
            Width = new LiteralOrPercentage(100, true)
        };
        stack.Children = [child];

        stack.Layout();

        Assert.Equal(80, child.Width); // Width - 2 * Padding = 100 - 20 = 80
    }

    [Fact]
    public void TestAutoHeightInHorizontalStack()
    {
        var context = new TestUIContext();
        var stack = new StackPanel(context)
        {
            Direction = LayoutDirection.Horizontal,
            Padding = 10,
            Width = 200,
            Height = 100
        };

        var child = new TestElement(context, 50)
        {
            Height = new LiteralOrPercentage(100, true)
        };
        stack.Children = [child];

        stack.Layout();

        Assert.Equal(80, child.Height); // Height - 2 * Padding = 100 - 20 = 80
    }
}

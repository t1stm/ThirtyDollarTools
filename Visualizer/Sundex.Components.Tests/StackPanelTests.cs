using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class StackPanelTests
{
    private class TestElement : UIElement
    {
        public TestElement(UIContext context, float width = 0, float height = 0) : base(context)
        {
            Width = width;
            Height = height;
        }

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

        Assert.Equal(0, child1.Computed.X);
        Assert.Equal(0, child1.Computed.Y);
        Assert.Equal(0, child2.Computed.X);
        Assert.Equal(30, child2.Computed.Y);
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

        Assert.Equal(0, child1.Computed.X);
        Assert.Equal(0, child1.Computed.Y);
        Assert.Equal(50, child2.Computed.X);
        Assert.Equal(0, child2.Computed.Y);
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

        Assert.Equal(10, child1.Computed.AbsoluteX - stack.Computed.AbsoluteX);
        Assert.Equal(10, child1.Computed.AbsoluteY - stack.Computed.AbsoluteY);
        Assert.Equal(10, child2.Computed.AbsoluteX - stack.Computed.AbsoluteX);
        Assert.Equal(10 + 30 + 5, child2.Computed.AbsoluteY - stack.Computed.AbsoluteY); // Padding + child1.Height + Spacing
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
            Width = new LiteralOrComputable(100, true)
        };
        stack.Children = [child];

        stack.Layout();

        Assert.Equal(80, child.Computed.Width); // Width - 2 * Padding = 100 - 20 = 80
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
            Height = new LiteralOrComputable(100, true)
        };
        stack.Children = [child];

        stack.Layout();

        Assert.Equal(80, child.Computed.Height); // Height - 2 * Padding = 100 - 20 = 80
    }
}

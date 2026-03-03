using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Xunit;

namespace Sundex.Components.Tests;

public class PanelTests
{
    private class TestElement(UIContext context)
        : UIElement(context)
    {
        public override string Tag => "test";
        protected override void DrawSelf(UIContext context) { }
    }

    [Fact]
    public void TestAddChild_SetsParent()
    {
        var context = new TestUIContext();
        var panel = new Panel(context);
        var child = new TestElement(context);

        panel.AddChild(child);

        Assert.Equal(panel, child.Parent);
        Assert.Contains(child, panel.Children);
    }

    [Fact]
    public void TestChildrenSetter_SetsParent()
    {
        var context = new TestUIContext();
        var panel = new Panel(context);
        var child = new TestElement(context);

        panel.Children = [child];

        Assert.Equal(panel, child.Parent);
        Assert.Single(panel.Children);
    }

    [Fact]
    public void TestLayout_SetsViewport()
    {
        var context = new TestUIContext();
        var panel = new Panel(context) { X = 10, Y = 20, Width = 100, Height = 200 };

        panel.Layout();

        Assert.NotNull(panel.Viewport);
        var viewport = panel.Viewport.Value;
        Assert.Equal(10, viewport.X); // left
        Assert.Equal(20, viewport.Y); // top
        Assert.Equal(110, viewport.Z); // right (10 + 100)
        Assert.Equal(220, viewport.W); // bottom (20 + 200)
    }

    [Fact]
    public void TestLayout_PropagatesToChildren()
    {
        var context = new TestUIContext();
        var panel = new Panel(context) { Width = 100, Height = 100 };
        var child = new TestElement(context)
        {
            X = 10,
            Y = 10,
            Width = 50,
            Height = 50
        };
        panel.AddChild(child);

        // Child should start with NeedsLayout = true
        Assert.True(child.NeedsLayout);

        panel.Layout();

        Assert.False(child.NeedsLayout);
        Assert.Equal(10, child.Computed.AbsoluteX);
        Assert.Equal(10, child.Computed.AbsoluteY);
    }
}

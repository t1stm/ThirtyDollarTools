using Components.Abstractions;
using Xunit;

namespace Components.Tests;

public class UIElementTests
{
    private class TestElement(UIContext context, float x = 0, float y = 0, float width = 0, float height = 0)
        : UIElement(context, x, y, width, height)
    {
        protected override void DrawSelf(UIContext context) { }
    }

    [Fact]
    public void TestAbsoluteCoordinates_NoParent()
    {
        var context = new TestUIContext();
        var element = new TestElement(context, 10, 20);

        Assert.Equal(10, element.AbsoluteX);
        Assert.Equal(20, element.AbsoluteY);
    }

    [Fact]
    public void TestAbsoluteCoordinates_WithParent()
    {
        var context = new TestUIContext();
        var parent = new TestElement(context, 100, 200);
        var child = new TestElement(context, 10, 20) { Parent = parent };

        Assert.Equal(110, child.AbsoluteX);
        Assert.Equal(220, child.AbsoluteY);
    }

    [Fact]
    public void TestAbsoluteCoordinates_Nested()
    {
        var context = new TestUIContext();
        var root = new TestElement(context, 100, 100);
        var middle = new TestElement(context, 50, 50) { Parent = root };
        var leaf = new TestElement(context, 10, 10) { Parent = middle };

        Assert.Equal(160, leaf.AbsoluteX);
        Assert.Equal(160, leaf.AbsoluteY);
    }

    [Fact]
    public void TestInvalidateCoordinates_PropagatesToChildren()
    {
        // UIElement doesn't have Children list, but Panel does. 
        // We'll test Panel later. 
        // For UIElement, we can test that setting X/Y invalidates coordinates.
        var context = new TestUIContext();
        var element = new TestElement(context, 10, 10);
        
        // Access AbsoluteX to clear dirty flag
        _ = element.AbsoluteX;
        
        element.X = 20;
        Assert.Equal(20, element.AbsoluteX);
    }
}

using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;
using Xunit;

namespace Sundex.Components.Tests;

public class WindowFrameTests
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
    public void TestWindowFrameInitialization()
    {
        var context = new TestUIContext();
        var window = new WindowFrame(context)
        {
            X = 100,
            Y = 100,
            Width = 400,
            Height = 300
        };

        Assert.Equal(100, window.X);
        Assert.Equal(100, window.Y);
        Assert.Equal(400, window.Width);
        Assert.Equal(300, window.Height);
    }

    [Fact]
    public void TestWindowFrameSetChild()
    {
        var context = new TestUIContext();
        var window = new WindowFrame(context)
        {
            X = 100,
            Y = 100,
            Width = 400,
            Height = 300
        };
        var child = new TestElement(context, 200, 200);
        
        window.Child = child;
        
        Assert.Equal(child, window.Child);
        // Container has Header and Child
    }

    [Fact]
    public void TestWindowFrameSizePropagation()
    {
        var context = new TestUIContext();
        var window = new WindowFrame(context)
        {
            X = 0,
            Y = 0,
            Width = 400,
            Height = 300,
        };
        
        window.Width = 500;
        window.Height = 400;
        
        Assert.Equal(500, window.Width);
        Assert.Equal(400, window.Height);
    }
}

using Xunit;
using Sundex.Components.Panels;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Cameras;
using Shared;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Core.Animations;

namespace Sundex.Components.Tests;

public class LayoutPaddingTests
{
    private class TestContext : UIContext
    {
        public List<IRenderable> QueuedRenderables = new();

        public TestContext()
        {
            Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
        }

        public new void QueueRender(IRenderable renderable, int renderIndex, int queueIndex = -1)
        {
            QueuedRenderables.Add(renderable);
            base.QueueRender(renderable, renderIndex, queueIndex);
        }
    }

    private class TestPanel : Panel
    {
        public TestPanel(UIContext context) : base(context) { }
        public IRenderable TestRenderable = new MockRenderable();

        protected override void DrawSelf(UIContext context)
        {
            base.DrawSelf(context);
            context.QueueRender(TestRenderable, Index);
        }
    }

    private class MockRenderable : IRenderable
    {
        public void Render(Camera camera) { }
        public void UpdateModel(bool isScreenSpace, ReadOnlySpan<Animation> animations) { }
    }

    [Fact]
    public void UIElement_DrawTo_ShouldEnsureLayoutBeforeQueuing()
    {
        // Arrange
        var context = new TestContext { Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080)) };
        var parent = new Panel(context)
        {
            Width = 100,
            Height = 100,
            Padding = 10
        };
        
        var child = new TestPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 10
        };
        
        parent.AddChild(child);
        
        // Assert initial state
        Assert.True(parent.NeedsLayout);
        Assert.True(child.NeedsLayout);
        
        // Act
        parent.DrawTo(context);
        
        // Assert
        Assert.False(parent.NeedsLayout);
        Assert.False(child.NeedsLayout);
        
        // Coordinates should be correctly calculated before DrawSelf was called
        // Parent Padding = 10, so Child AbsoluteX should be 10.
        Assert.Equal(10, child.Computed.AbsoluteX);
        Assert.Equal(10, child.Computed.AbsoluteY);
        Assert.Equal(80, child.Computed.Width);
    }

    [Fact]
    public void StackPanel_Vertical_ChildWith100PercentWidth_ShouldRespectPadding()
    {
        // Arrange
        var context = new TestContext { Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080)) };
        var parent = new StackPanel(context)
        {
            Width = 100,
            Height = 100,
            Padding = 10,
            Direction = LayoutDirection.Vertical
        };
        
        var child = new Panel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 10
        };
        
        parent.AddChild(child);
        
        // Act
        parent.Layout();
        
        // Assert
        // Parent Width = 100. Padding = 10. Inner Width = 100 - 2 * 10 = 80.
        // Child Width = 100% of Inner Width = 80.
        Assert.Equal(80, child.Computed.Width);
        // AbsoluteX = ParentAbsX(0) + ParentPadding(10) + ChildX(0) = 10.
        Assert.Equal(10, child.Computed.AbsoluteX);
    }

    [Fact]
    public void Panel_AbsoluteCoordinates_ShouldRespectParentPadding()
    {
        // Arrange
        var context = new TestContext { Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080)) };
        var parent = new StackPanel(context)
        {
            Width = 100,
            Height = 100,
            Padding = 10,
            Direction = LayoutDirection.Vertical
        };
        
        var child = new Panel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 10
        };
        
        parent.AddChild(child);
        
        // Act
        parent.Layout();
        
        // Assert
        // Parent AbsoluteX/Y is 0 (root).
        // Parent Padding is 10.
        // Child relative X is 0 (from StackPanel layout).
        // Child AbsoluteX should be Parent AbsX + Padding + X = 0 + 10 + 0 = 10.
        // IT PASSES, which means X=0 and Padding=10 results in AbsoluteX=10.
        Assert.Equal(10, child.Computed.AbsoluteX);
        Assert.Equal(10, child.Computed.AbsoluteY);
    }

    [Fact]
    public void FlexPanel_Horizontal_ChildWith100PercentWidth_ShouldRespectPadding()
    {
        // Arrange
        var context = new TestContext { Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080)) };
        var parent = new FlexPanel(context)
        {
            Width = 100,
            Height = 100,
            Padding = 10,
            Direction = LayoutDirection.Horizontal
        };
        
        var child = new Panel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 10
        };
        
        parent.AddChild(child);
        
        // Act
        parent.Layout();
        
        // Assert
        // Parent Width = 100. Padding = 10. Inner Width = 80.
        // Child Width = 100% of free space = 80.
        Assert.Equal(80, child.Computed.Width);
    }
    [Fact]
    public void UIElement_NestedLayout_ShouldUpdateWhenParentMoves()
    {
        // Arrange
        var context = new TestContext { Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080)) };
        var root = new Panel(context)
        {
            Width = 1000,
            Height = 1000
        };
        
        var parent = new Panel(context)
        {
            X = 100,
            Y = 100,
            Width = 500,
            Height = 500
        };
        
        var child = new Panel(context)
        {
            X = 50,
            Y = 50,
            Width = 100,
            Height = 100
        };
        
        root.AddChild(parent);
        parent.AddChild(child);
        
        // Act 1: Initial Layout
        root.Layout();
        
        // Assert initial coordinates
        Assert.Equal(100, parent.Computed.AbsoluteX);
        Assert.Equal(150, child.Computed.AbsoluteX); // Parent X(100) + Parent Padding(0) + Child X(50)
        
        // Act 2: Move parent
        parent.X = 200;
        // In reality, setting X should call InvalidateLayout which sets NeedsLayout = true on parent and notifies root.
        
        root.Layout();
        
        // Assert updated coordinates
        Assert.Equal(200, parent.Computed.AbsoluteX);
        Assert.Equal(250, child.Computed.AbsoluteX); // Parent X(200) + Parent Padding(0) + Child X(50)
    }

    [Fact]
    public void UIElement_RootlessLayout_ShouldUpdateWhenAddedToParent()
    {
        // Arrange
        var context = new TestContext { Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080)) };
        var child = new Panel(context)
        {
            X = 50,
            Y = 50,
            Width = 100,
            Height = 100
        };
        
        // Act 1: Layout without parent (uses viewport)
        child.Layout();
        Assert.Equal(50, child.Computed.AbsoluteX);
        
        // Act 2: Add to parent
        var parent = new Panel(context)
        {
            X = 100,
            Y = 100,
            Width = 500,
            Height = 500
        };
        parent.AddChild(child);
        
        parent.Layout();
        
        // Assert updated coordinates
        Assert.Equal(150, child.Computed.AbsoluteX);
    }
}

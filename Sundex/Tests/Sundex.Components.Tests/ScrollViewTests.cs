using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;

namespace Sundex.Components.Tests;

public class ScrollViewTests
{
    // View at (10,10), 200x100 -> bounds x 10..210, y 10..110.
    // Bar is 20 wide at the right edge: x 190..210.
    private static (TestUIContext ctx, Panel root, ScrollView view) NewView(int rows, float rowHeight = 30)
    {
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };
        var view = new ScrollView(ctx) { X = 10, Y = 10, Width = 200, Height = 100 };
        for (var i = 0; i < rows; i++) view.Children.Add(NewRow(ctx, rowHeight));
        view.Children = view.Children; // reassign to re-parent
        root.Children = [view];
        root.Layout();
        return (ctx, root, view);
    }

    private static Panel NewRow(TestUIContext ctx, float height)
    {
        return new Panel(ctx)
        {
            Height = height,
            Width = LiteralOrComputable.Percent(100),
            Background = new ColoredPlane()
        };
    }

    private static void Scroll(TestUIContext ctx, Panel root, float deltaY)
    {
        ctx.UpdatePointer(root, 50, 50, false, false, false, new Vector2(0, deltaY));
    }

    [Fact]
    public void Layout_ComputesContentAndMaxScroll()
    {
        var (_, _, view) = NewView(10);
        Assert.Equal(300, view.ContentHeight);
        Assert.Equal(200, view.MaxScroll);

        var firstRow = view.Children[0];
        Assert.Equal(10, firstRow.Computed.AbsoluteY);
    }

    [Fact]
    public void Wheel_ScrollsAndClamps()
    {
        var (ctx, root, view) = NewView(10);

        Scroll(ctx, root, -1); // wheel down one notch
        Assert.Equal(48, view.ScrollY);

        root.Layout();
        Assert.Equal(10 - 48, view.Children[0].Computed.AbsoluteY);

        Scroll(ctx, root, -100); // way past the end
        Assert.Equal(200, view.ScrollY);

        Scroll(ctx, root, 100); // way back up
        Assert.Equal(0, view.ScrollY);
    }

    [Fact]
    public void Wheel_IgnoredWhenContentFits()
    {
        var (ctx, root, view) = NewView(2);
        Assert.Equal(0, view.MaxScroll);

        Scroll(ctx, root, -1);
        Assert.Equal(0, view.ScrollY);
    }

    [Fact]
    public void Children_AreClippedToTheViewBounds()
    {
        var (_, _, view) = NewView(10);

        var clip = new Vector4i(10, 10, 210, 110);
        Assert.Equal(clip, ((ColoredPlane)((Panel)view.Children[0]).Background!).ClipRect);
        Assert.Equal(clip, ((ColoredPlane)((Panel)view.Children[9]).Background!).ClipRect);
    }

    [Fact]
    public void HitTest_DoesNotReachOverflowingChildren()
    {
        var (ctx, root, view) = NewView(10);
        var clicks = 0;
        view.Children[5].OnClick = _ => clicks++; // spans y 160..190, below the view

        ctx.UpdatePointer(root, 50, 165, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, 50, 165, false, false, true, Vector2.Zero);
        Assert.Equal(0, clicks);

        // Scroll it into view (y 160..190 - 150 = 10..40) and click again.
        view.ScrollY = 150;
        root.Layout();
        ctx.UpdatePointer(root, 50, 20, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(root, 50, 20, false, false, true, Vector2.Zero);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ThumbDrag_ScrollsTheContent()
    {
        var (ctx, root, view) = NewView(10);

        // Thumb is 33.3px (100/300 of the 100px track), sitting at the top.
        ctx.UpdatePointer(root, 200, 20, true, true, false, Vector2.Zero); // press the thumb
        ctx.UpdatePointer(root, 200, 110, true, false, false, Vector2.Zero); // drag to the bottom
        Assert.Equal(200, view.ScrollY);

        root.Layout();
        Assert.Equal(10 - 200, view.Children[0].Computed.AbsoluteY);

        ctx.UpdatePointer(root, 200, 110, false, false, true, Vector2.Zero);
        Assert.Equal(200, view.ScrollY); // release keeps the position
    }

    [Fact]
    public void DynamicallyAddedRows_StackAndScroll()
    {
        var (ctx, root, view) = NewView(0);
        for (var i = 0; i < 100; i++) view.AddChild(NewRow(ctx, 30));
        root.Layout();

        Assert.Equal(3000, view.ContentHeight);
        Assert.Equal(2900, view.MaxScroll);
        Assert.Equal(10 + 99 * 30, view.Children[99].Computed.AbsoluteY);

        view.ScrollY = 2900;
        root.Layout();
        Assert.Equal(110 - 30, view.Children[99].Computed.AbsoluteY);
        Assert.Equal(new Vector4i(10, 10, 210, 110),
            ((ColoredPlane)((Panel)view.Children[99]).Background!).ClipRect);
    }

    [Fact]
    public void TextInput_ClipsItsContentToItsBox()
    {
        // The self-clip lands on all content children — observable via a spinner button.
        var ctx = new TestUIContext();
        var root = new Panel(ctx) { Width = 800, Height = 600 };
        var input = new NumericInput(ctx) { X = 10, Y = 10, Width = 200 };
        root.Children = [input];
        root.Layout();

        var button = (Panel)input.Children[1];
        Assert.Equal(new Vector4i(10, 10, 210, 42), ((ColoredPlane)button.Background!).ClipRect);
    }
}

using System.Linq;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class FlexPanelTests
{
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
        child1.Parent = flex;
        child2.Parent = flex;

        flex.Layout();

        // innerWidth = 100. total_fixed = 30. total_spacing = 0.
        // free_space = 70. flex_size = 70 / 1 = 70.
        Assert.Equal(70, child1.Computed.Width);
        Assert.Equal(30, child2.Computed.Width);
    }

    [Fact]
    public void TestHorizontalLayout_Wrap()
    {
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 100,
            Height = 100,
            Direction = LayoutDirection.Horizontal,
            Wrap = true,
            Spacing = 10,
            Padding = 0
        };
        var child1 = new TestElement(context) { Width = 40, Height = 20 };
        var child2 = new TestElement(context) { Width = 40, Height = 20 };
        var child3 = new TestElement(context) { Width = 40, Height = 20 };
        flex.Children = [child1, child2, child3];

        flex.Layout();

        // child1: X=0, Y=0. currentX becomes 40+10=50.
        // child2: X=50, Y=0. currentX becomes 50+40+10=100.
        // child3: 100 + 40 > 100 (if Spacing is added? potentialWidth = 100+40 = 140).
        // It wraps. X=0, Y=20+10=30.
        Assert.Equal(0, child1.X);
        Assert.Equal(0, child1.Y);
        Assert.Equal(50, child2.X);
        Assert.Equal(0, child2.Y);
        Assert.Equal(0, child3.X);
        Assert.Equal(30, child3.Y);
    }

    [Fact]
    public void TestHorizontalLayout_Wrap_MeasureMatchesActualRowCountAcrossMultipleWraps()
    {
        // Regression test: after the first wrap, Measure() used to keep treating the new
        // line's first item as still-pending ("firstInLine = true"), so the *next* item
        // silently replaced its width contribution instead of adding to it. That let every
        // row past the first pack one extra item before wrapping, under-reporting the
        // total height once there were enough rows for it to show (2+ wraps).
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 90,
            Height = LiteralOrComputable.AutoSize,
            Direction = LayoutDirection.Horizontal,
            Wrap = true,
            Spacing = 10,
            Padding = 0
        };
        // Row capacity is 2 (40+10+40=90 fits, a 3rd would need 140). 6 items -> 3 rows of 2.
        var children = Enumerable.Range(0, 6)
            .Select(_ => new TestElement(context) { Width = 40, Height = 20 })
            .ToList<UIElement>();
        flex.Children = children;

        var (_, measuredHeight) = flex.Measure(90, 1000);
        Assert.Equal(80, measuredHeight); // 3 rows * 20 + 2 gaps * 10

        flex.Layout();
        Assert.Equal(80, flex.Computed.Height);
        Assert.Equal(60, children[4].Y); // third row
        Assert.Equal(0, children[4].X);
        Assert.Equal(50, children[5].X);
    }

    [Fact]
    public void TestVerticalLayout_Wrap()
    {
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 100,
            Height = 100,
            Direction = LayoutDirection.Vertical,
            Wrap = true,
            Spacing = 10,
            Padding = 0
        };
        var child1 = new TestElement(context) { Width = 20, Height = 50 };
        var child2 = new TestElement(context) { Width = 20, Height = 50 };
        flex.Children = [child1, child2];

        flex.Layout();

        // child1: X=0, Y=0. currentY becomes 50+10=60.
        // child2: potentialHeight = 60 + 50 = 110 > 100. Wraps.
        // child2: X=30, Y=0.
        Assert.Equal(0, child1.X);
        Assert.Equal(0, child1.Y);
        Assert.Equal(30, child2.X);
        Assert.Equal(0, child2.Y);
    }

    [Fact]
    public void TestVerticalLayout_Wrap_MeasureMatchesActualColumnCountAcrossMultipleWraps()
    {
        // Same regression as the horizontal case, mirrored for columns.
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = LiteralOrComputable.AutoSize,
            Height = 90,
            Direction = LayoutDirection.Vertical,
            Wrap = true,
            Spacing = 10,
            Padding = 0
        };
        // Column capacity is 2 (40+10+40=90 fits). 6 items -> 3 columns of 2.
        var children = Enumerable.Range(0, 6)
            .Select(_ => new TestElement(context) { Width = 20, Height = 40 })
            .ToList<UIElement>();
        flex.Children = children;

        var (measuredWidth, _) = flex.Measure(1000, 90);
        Assert.Equal(80, measuredWidth); // 3 columns * 20 + 2 gaps * 10

        flex.Layout();
        Assert.Equal(80, flex.Computed.Width);
        Assert.Equal(60, children[4].X); // third column
        Assert.Equal(0, children[4].Y);
        Assert.Equal(50, children[5].Y);
    }

    [Fact]
    public void PercentChild_KeepsItsDeclaration_AndReflowsOnResize()
    {
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 400,
            Height = 1000,
            Direction = LayoutDirection.Vertical
        };
        var header = new TestElement(context) { Width = 400, Height = 56 };
        var body = new TestElement(context) { Width = 400, Height = new LiteralOrComputable(100, true) };
        flex.Children = [header, body];

        flex.Layout();

        // First pass: body takes all the free space below the header.
        Assert.Equal(944, body.Computed.Height);
        // The declaration must survive the pass — resolving it may not overwrite it.
        Assert.True(body.Height.IsPercentage);

        // The scene-resize path (Resize handlers do InvalidateCoordinates + Layout).
        flex.Height = 700;
        flex.InvalidateCoordinates();
        flex.Layout();

        Assert.Equal(644, body.Computed.Height);

        flex.Height = 1400;
        flex.InvalidateCoordinates();
        flex.Layout();

        Assert.Equal(1344, body.Computed.Height);
    }

    [Fact]
    public void PercentChildren_ShareTheFreeSpace_OnEveryPass()
    {
        var context = new TestUIContext();
        var flex = new FlexPanel(context)
        {
            Width = 300,
            Height = 100,
            Direction = LayoutDirection.Horizontal
        };
        var sidebar = new TestElement(context) { Width = 100, Height = 20 };
        var wide = new TestElement(context) { Width = new LiteralOrComputable(75, true), Height = 20 };
        var narrow = new TestElement(context) { Width = new LiteralOrComputable(25, true), Height = 20 };
        flex.Children = [sidebar, wide, narrow];

        flex.Layout();

        // free space = 300 - 100 = 200 → 150/50 split after the fixed sidebar.
        Assert.Equal(150, wide.Computed.Width);
        Assert.Equal(50, narrow.Computed.Width);
        Assert.Equal(100, wide.X);
        Assert.Equal(250, narrow.X);

        flex.Width = 500;
        flex.InvalidateCoordinates();
        flex.Layout();

        // free space = 400 → the split must follow.
        Assert.Equal(300, wide.Computed.Width);
        Assert.Equal(100, narrow.Computed.Width);
        Assert.Equal(400, narrow.X);
    }

    private class TestElement(UIContext context)
        : UIElement(context)
    {
        public override string Tag => "test";

        protected override void DrawSelf(UIContext context)
        {
        }
    }
}
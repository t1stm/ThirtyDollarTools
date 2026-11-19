using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

/// <summary>The CSS-flex settings FlexPanel gained in the fork: everything the port's own Box needed.</summary>
public class FlexLayoutTests
{
    private static Block Fixed(UIContext context, float width, float height) => new(context) { Width = width, Height = height };

    private static FlexPanel Panel(UIContext context, float width, float height, LayoutDirection direction) =>
        new(context) { Width = width, Height = height, Direction = direction };

    [Fact]
    public void PerSidePaddingAndGapsPlaceChildren()
    {
        var context = new TestUIContext();
        var row = Panel(context, 200, 50, LayoutDirection.Horizontal);
        row.PaddingLeft = 10;
        row.PaddingTop = 5;
        row.ColumnGap = 7;
        var first = Fixed(context, 20, 10);
        var second = Fixed(context, 20, 10);
        row.Children = [first, second];

        row.Layout();

        Assert.Equal(10, first.Computed.X);
        Assert.Equal(5, first.Computed.Y);
        Assert.Equal(37, second.Computed.X);
    }

    [Fact]
    public void ABorderTakesLayoutSpaceLikeCss()
    {
        var context = new TestUIContext();
        var outer = Panel(context, 200, 100, LayoutDirection.Vertical);
        outer.AlignItems = Align.Start;
        var box = new FlexPanel(context) { Direction = LayoutDirection.Horizontal, Padding = 4, BorderWidth = 1 };
        var child = Fixed(context, 20, 10);
        box.Children = [child];
        outer.Children = [box];

        outer.Layout();

        Assert.Equal(30, box.Computed.Width);
        Assert.Equal(20, box.Computed.Height);
        Assert.Equal(5, child.Computed.X);
        Assert.Equal(5, child.Computed.Y);
    }

    [Fact]
    public void JustifyBetweenSpreadsTheFreeSpace()
    {
        var context = new TestUIContext();
        var row = Panel(context, 100, 20, LayoutDirection.Horizontal);
        row.Justify = Justify.Between;
        var first = Fixed(context, 20, 10);
        var second = Fixed(context, 20, 10);
        var third = Fixed(context, 20, 10);
        row.Children = [first, second, third];

        row.Layout();

        Assert.Equal(0, first.Computed.X);
        Assert.Equal(40, second.Computed.X);
        Assert.Equal(80, third.Computed.X);
    }

    [Fact]
    public void AlignItemsStretchFillsTheCrossAxisAndAlignSelfOverridesIt()
    {
        var context = new TestUIContext();
        var row = Panel(context, 100, 40, LayoutDirection.Horizontal);
        row.AlignItems = Align.Stretch;
        var tall = new Block(context) { Width = 20, Height = LiteralOrComputable.AutoSize };
        var centred = new Block(context) { Width = 20, Height = 10, AlignSelf = SelfAlign.Center };
        row.Children = [tall, centred];

        row.Layout();

        Assert.Equal(40, tall.Computed.Height);
        Assert.Equal(15, centred.Computed.Y);
    }

    [Fact]
    public void GrowSharesTheRestAndOnlyWhatMayCollapseShrinks()
    {
        var context = new TestUIContext();
        var row = Panel(context, 100, 20, LayoutDirection.Horizontal);
        var fixedOne = Fixed(context, 30, 10);
        var grows = new Block(context) { Width = 10, Height = 10, Grow = 1 };
        row.Children = [fixedOne, grows];
        row.Layout();
        Assert.Equal(70, grows.Computed.Width);

        var narrow = Panel(context, 50, 20, LayoutDirection.Horizontal);
        var stays = Fixed(context, 40, 10);
        var collapses = new Block(context) { Width = 40, Height = 10, MinWidth = 0 };
        narrow.Children = [stays, collapses];
        narrow.Layout();
        Assert.Equal(40, stays.Computed.Width);
        Assert.Equal(10, collapses.Computed.Width);
    }

    [Fact]
    public void AnAutoMarginPushesAChildToTheFarEnd()
    {
        var context = new TestUIContext();
        var row = Panel(context, 100, 20, LayoutDirection.Horizontal);
        var left = Fixed(context, 20, 10);
        var right = new Block(context) { Width = 20, Height = 10, MarginLeft = LiteralOrComputable.AutoSize };
        row.Children = [left, right];

        row.Layout();

        Assert.Equal(80, right.Computed.X);
    }

    [Fact]
    public void AnAbsoluteChildIsPlacedByItsInsetsAndTakesNoFlowSpace()
    {
        var context = new TestUIContext();
        var column = Panel(context, 100, 100, LayoutDirection.Vertical);
        var badge = new Block(context)
        {
            Position = Placement.Absolute, Width = 10, Height = 10, Right = 5, Bottom = 5
        };
        var overlay = new Block(context)
        {
            Position = Placement.Absolute, Width = LiteralOrComputable.AutoSize, Height = LiteralOrComputable.AutoSize,
            Left = 0, Right = 0, Top = 0, Bottom = 0
        };
        var first = Fixed(context, 20, 20);
        column.Children = [badge, overlay, first];

        column.Layout();

        Assert.Equal(85, badge.Computed.X);
        Assert.Equal(85, badge.Computed.Y);
        Assert.Equal(100, overlay.Computed.Width);
        Assert.Equal(100, overlay.Computed.Height);
        Assert.Equal(0, first.Computed.Y);
    }

    [Fact]
    public void AScrollingColumnOffsetsItsChildrenAndClampsTheOffset()
    {
        var context = new TestUIContext();
        var column = Panel(context, 100, 50, LayoutDirection.Vertical);
        column.OverflowY = Overflow.Scroll;
        var rows = Enumerable.Range(0, 5).Select(_ => Fixed(context, 100, 20)).ToList();
        column.Children = [..rows];
        column.Layout();
        Assert.Equal(50, column.MaxScrollY);

        column.ScrollY = 30;
        column.Layout();
        Assert.Equal(-30, rows[0].Computed.Y);

        column.ScrollY = 500;
        Assert.Equal(50, column.ScrollY);
    }

    [Fact]
    public void ARegionsChildrenAreLaidOutAsItsParentsOwn()
    {
        var context = new TestUIContext();
        var row = Panel(context, 200, 20, LayoutDirection.Horizontal);
        row.ColumnGap = 10;
        var before = Fixed(context, 20, 10);
        var region = new Region(context);
        var inside = Fixed(context, 30, 10);
        var after = Fixed(context, 20, 10);
        region.AddChild(inside);
        row.Children = [before, region, after];

        row.Layout();

        Assert.Equal(30, inside.Computed.AbsoluteX);
        Assert.Equal(70, after.Computed.AbsoluteX);
    }

    [Fact]
    public void AspectRatioGivesTheOtherSide()
    {
        var context = new TestUIContext();
        var column = Panel(context, 100, 200, LayoutDirection.Vertical);
        column.AlignItems = Align.Stretch;
        var cover = new Block(context)
        {
            Width = LiteralOrComputable.AutoSize, Height = LiteralOrComputable.AutoSize, AspectRatio = 1
        };
        column.Children = [cover];

        column.Layout();

        Assert.Equal(100, cover.Computed.Height);
    }

    [Fact]
    public void AContentSizedPanelFollowsAChildThatGrows()
    {
        var context = new TestUIContext();
        var outer = Panel(context, 300, 100, LayoutDirection.Horizontal);
        var inner = new FlexPanel(context) { Direction = LayoutDirection.Horizontal };
        var child = Fixed(context, 20, 10);
        inner.Children = [child];
        outer.Children = [inner];
        outer.Layout();
        Assert.Equal(20, inner.Computed.Width);

        // The measurement cached for the old width has to go, all the way up.
        child.Width = 60;
        outer.Layout();
        Assert.Equal(60, inner.Computed.Width);
    }

    [Fact]
    public void AnInvalidationDuringAParentsLayoutIsNotLost()
    {
        var context = new TestUIContext();
        var outer = Panel(context, 300, 100, LayoutDirection.Horizontal);
        var inner = new FlexPanel(context) { Direction = LayoutDirection.Horizontal };
        var child = Fixed(context, 20, 10);
        inner.Children = [child];
        var trigger = Fixed(context, 10, 10);
        outer.Children = [inner, trigger];
        outer.Layout();

        // After the inner panel's turn, something resizes its child mid-pass: the next pass
        // must still reach it rather than leave it marked under a clean parent.
        outer.AfterLayout = () =>
        {
            outer.AfterLayout = null;
            child.Width = 50;
        };
        outer.InvalidateLayout();
        outer.Layout();
        Assert.True(outer.NeedsLayout);

        outer.Layout();
        Assert.Equal(50, inner.Computed.Width);
    }

    private sealed class Block(UIContext context) : UIElement(context)
    {
        public override string Tag => "block";

        protected override void DrawSelf(UIContext context)
        {
        }
    }
}

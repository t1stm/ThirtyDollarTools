using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class TableTests
{
    private static TableCell Cell(TestUIContext ctx, float width, float height, bool span = false) =>
        new(ctx) { Span = span, Children = [new FlexPanel(ctx) { Width = width, Height = height }] };

    [Fact]
    public void ColumnsLineUpAcrossRows_AndShareTheSpareRoomByWidth()
    {
        var ctx = new TestUIContext();
        var table = new Table(ctx) { Width = 400, Columns = 2 };
        var first = Cell(ctx, 50, 10);
        var second = Cell(ctx, 150, 20);
        var third = Cell(ctx, 100, 10);
        var fourth = Cell(ctx, 50, 10);
        foreach (var cell in new[] { first, second, third, fourth }) table.AddChild(cell);
        table.Layout();

        // Natural widths 100 and 150 share 400 as 160 and 240.
        Assert.Equal(160, first.Computed.Width, 1);
        Assert.Equal(160, third.Computed.Width, 1);
        Assert.Equal(160, second.Computed.X, 1);
        Assert.Equal(160, fourth.Computed.X, 1);
        // The first row is as tall as its tallest cell.
        Assert.Equal(20, first.Computed.Height, 1);
        Assert.Equal(20, third.Computed.Y, 1);
    }

    [Fact]
    public void ABorderedTableFitsInsideItsBorder()
    {
        // Borders take layout space: columns sized to the padding alone overflowed by the border,
        // drew a scroll thumb and let the wheel slide the table sideways.
        var ctx = new TestUIContext();
        var table = new Table(ctx) { Width = 400, Columns = 2, BorderWidth = 1, OverflowX = Overflow.Scroll };
        var first = Cell(ctx, 50, 10);
        var second = Cell(ctx, 50, 10);
        table.AddChild(first);
        table.AddChild(second);
        table.Layout();

        Assert.Equal(1, first.Computed.X, 1);
        Assert.Equal(1, first.Computed.Y, 1);
        Assert.Equal(399, second.Computed.X + second.Computed.Width, 1);
        Assert.Equal(0, table.MaxScrollX, 1);
    }

    [Fact]
    public void ASpanTakesAWholeRow()
    {
        var ctx = new TestUIContext();
        var table = new Table(ctx) { Width = 300, Columns = 3 };
        var span = Cell(ctx, 10, 15, span: true);
        var after = Cell(ctx, 10, 10);
        table.AddChild(span);
        table.AddChild(after);
        table.Layout();
        Assert.Equal(300, span.Computed.Width, 1);
        Assert.Equal(15, after.Computed.Y, 1);
        Assert.Equal(0, after.Computed.X, 1);
    }
}

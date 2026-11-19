using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class GridTests
{
    [Fact]
    public void FitsAsManyColumnsAsTheMinimumAllows_AndStretchesThem()
    {
        var ctx = new TestUIContext();
        var grid = new Grid(ctx) { Width = 500, Height = LiteralOrComputable.AutoSize, MinColumn = 150, ColumnGap = 10, RowGap = 4 };
        var cells = Enumerable.Range(0, 4).Select(index => new FlexPanel(ctx) { Height = 20 + index }).ToList();
        foreach (var cell in cells) grid.AddChild(cell);
        grid.Layout();

        // (500 + 10) / (150 + 10) = 3 columns of (500 - 20) / 3 = 160.
        Assert.Equal(160, cells[0].Computed.Width, 1);
        Assert.Equal(170, cells[1].Computed.X, 1);
        Assert.Equal(340, cells[2].Computed.X, 1);
        // A row is as tall as its tallest cell; the fourth starts the second row.
        Assert.Equal(22, cells[0].Computed.Height, 1);
        Assert.Equal(0, cells[3].Computed.X, 1);
        Assert.Equal(26, cells[3].Computed.Y, 1);
        Assert.Equal(22 + 4 + 23, grid.Computed.Height, 1);
    }

    [Fact]
    public void AFixedCountWins()
    {
        var ctx = new TestUIContext();
        var grid = new Grid(ctx) { Width = 400, Columns = 2 };
        var first = new FlexPanel(ctx) { Height = 10 };
        var second = new FlexPanel(ctx) { Height = 10 };
        grid.AddChild(first);
        grid.AddChild(second);
        grid.Layout();
        Assert.Equal(200, first.Computed.Width, 1);
        Assert.Equal(200, second.Computed.X, 1);
    }
}

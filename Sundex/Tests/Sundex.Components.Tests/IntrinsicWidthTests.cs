using Sundex.Components.Abstractions;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class IntrinsicWidthTests
{
    [Fact]
    public void AnAutoRowMeasuresItsContentNotWhatItsGrowingChildCouldFill()
    {
        var ctx = new TestUIContext();
        var row = new FlexPanel(ctx) { Direction = LayoutDirection.Horizontal };
        row.AddChild(new FlexPanel(ctx) { Width = 30, Height = 10, Grow = 1 });

        Assert.Equal(30, row.Measure(400, 100).width, 1);
    }

    [Fact]
    public void GrowingRowsShareARowByTheirContent()
    {
        var ctx = new TestUIContext();
        var outer = new FlexPanel(ctx) { Width = 300, Height = 50 };
        var cells = Enumerable.Range(0, 3).Select(_ =>
        {
            var cell = new FlexPanel(ctx) { Grow = 1 };
            cell.AddChild(new FlexPanel(ctx) { Grow = 1, Height = 10 });
            return cell;
        }).ToList();
        foreach (var cell in cells) outer.AddChild(cell);
        outer.Layout();

        Assert.All(cells, cell => Assert.Equal(100, cell.Computed.Width, 1));
        Assert.Equal(200, cells[2].Computed.X, 1);
    }
}

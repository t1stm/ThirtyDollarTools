using Sundex.Components.Labels;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class WrapLineTests
{
    [Fact]
    public void EachWrappedLineGrowsAndAlignsOnItsOwn()
    {
        var ctx = new TestUIContext();
        var row = new FlexPanel(ctx) { Width = 100, Wrap = true, AlignItems = Align.Center };
        var first = new FlexPanel(ctx) { Width = 40, Height = 10, Grow = 1 };
        var second = new FlexPanel(ctx) { Width = 40, Height = 20, Grow = 1 };
        var third = new FlexPanel(ctx) { Width = 40, Height = 10, Grow = 1 };
        row.AddChild(first);
        row.AddChild(second);
        row.AddChild(third);
        row.Layout();

        Assert.Equal(50, first.Computed.Width, 1);
        Assert.Equal(50, second.Computed.X, 1);
        Assert.Equal(5, first.Computed.Y, 1); // centred on a 20px line
        Assert.Equal(100, third.Computed.Width, 1);
        Assert.Equal(20, third.Computed.Y, 1);
        Assert.Equal(30, row.Computed.Height, 1);
    }

    [Fact]
    public void LabelledButtonsInAContentSizedWrapBoxShareALine()
    {
        // The bot's Set and Clear: a wrapping box sized to its content stacked them, because the
        // float sum it was measured with came out a hair under the one it was laid out with.
        var ctx = new TestUIContext();
        var row = new FlexPanel(ctx) { Width = 900, Wrap = true, AlignItems = Align.Center, Gap = 8 };
        row.AddChild(new FlexPanel(ctx) { Width = 320, Height = 42, Grow = 1, MaxWidth = 320 });
        var actions = new FlexPanel(ctx) { Wrap = true, AlignItems = Align.Start };
        Tooltip Tipped(string text) => new(ctx)
        {
            Children = [new Button(ctx, text) { PaddingLeft = 10, PaddingRight = 10, MinHeight = 28, MarginRight = 4, MarginBottom = 2 }]
        };
        var set = Tipped("Set");
        var clear = Tipped("Clear");
        actions.AddChild(set);
        var branch = new Region(ctx);
        actions.AddChild(branch);
        branch.AddChild(clear);
        row.AddChild(actions);
        row.Layout();

        Assert.Equal(set.Computed.Y, clear.Computed.Y, 1);
    }
}

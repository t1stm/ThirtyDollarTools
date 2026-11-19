using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;

namespace Sundex.Components.Tests;

public class AbsoluteInsetTests
{
    [Fact]
    public void APlainPanelWithBothInsetsFillsThem()
    {
        var ctx = new TestUIContext();
        var box = new FlexPanel(ctx) { Width = 200, Height = 100 };
        var cover = new Panel(ctx) { Position = Placement.Absolute, Left = 0, Top = 0, Right = 0, Bottom = 0 };
        box.AddChild(cover);
        box.Layout();
        Assert.Equal(200, cover.Computed.Width, 1);
        Assert.Equal(100, cover.Computed.Height, 1);
    }

    [Fact]
    public void BothInsetsAndAutoMarginsCentreANarrowerBox()
    {
        var ctx = new TestUIContext();
        var box = new FlexPanel(ctx) { Width = 400, Height = 100 };
        var bar = new FlexPanel(ctx)
        {
            Position = Placement.Absolute, Left = 0, Right = 0, Bottom = 10, Height = 20, MaxWidth = 200,
            MarginLeft = LiteralOrComputable.AutoSize, MarginRight = LiteralOrComputable.AutoSize
        };
        box.AddChild(bar);
        box.Layout();
        Assert.Equal(200, bar.Computed.Width, 1);
        Assert.Equal(100, bar.Computed.X, 1);
        Assert.Equal(70, bar.Computed.Y, 1);
    }
}

using EditorScene.Scenes.Components;
using OpenTK.Mathematics;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class InstrumentSelectorTests
{
    [Fact]
    public void Fill_ThenClickingARow_FiresOnPick()
    {
        var ctx = new EditorTestContext();
        var selector = new InstrumentSelector(ctx) { Width = 320, Height = 420 };
        var a = new Instrument { Name = "A" };
        selector.Fill([a]);
        selector.Layout();

        Instrument? picked = null;
        selector.OnPick = i => picked = i;

        var list = selector.Children.OfType<ScrollView>().Single();
        var row = list.Children.OfType<InstrumentRow>().Single();
        var x = row.Computed.AbsoluteX + 5;
        var y = row.Computed.AbsoluteY + row.Computed.Height / 2;
        ctx.UpdatePointer(selector, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(selector, x, y, false, false, true, Vector2.Zero);

        Assert.Same(a, picked);
    }

    [Fact]
    public void NewInstrumentRow_FiresOnNew()
    {
        var ctx = new EditorTestContext();
        var selector = new InstrumentSelector(ctx) { Width = 320, Height = 420 };
        selector.Fill([]);
        selector.Layout();

        var fired = 0;
        selector.OnNew = () => fired++;

        var list = selector.Children.OfType<ScrollView>().Single();
        var newRow = list.Children.Last(); // "+ New instrument" always trails the list
        var x = newRow.Computed.AbsoluteX + newRow.Computed.Width / 2;
        var y = newRow.Computed.AbsoluteY + newRow.Computed.Height / 2;
        ctx.UpdatePointer(selector, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(selector, x, y, false, false, true, Vector2.Zero);

        Assert.Equal(1, fired);
    }
}

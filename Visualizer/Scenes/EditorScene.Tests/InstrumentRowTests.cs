using EditorScene.Scenes.Components;
using OpenTK.Mathematics;
using Sundex.Components.Labels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class InstrumentRowTests
{
    [Fact]
    public void ClickingTheEditButton_FiresOnEdit_NotOnPick_AndClickingElsewhere_FiresOnPick()
    {
        var ctx = new EditorTestContext();
        var instrument = new Instrument { Name = "Layer" };
        Instrument? picked = null;
        Instrument? edited = null;
        var row = new InstrumentRow(ctx, instrument, i => picked = i, i => edited = i) { Width = 200 };
        row.Layout();

        var edit = row.Children.OfType<Button>().Single();
        var ex = edit.Computed.AbsoluteX + edit.Computed.Width / 2;
        var ey = edit.Computed.AbsoluteY + edit.Computed.Height / 2;
        ctx.UpdatePointer(row, ex, ey, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(row, ex, ey, false, false, true, Vector2.Zero);

        Assert.Same(instrument, edited);
        Assert.Null(picked);

        // Past the label and edit button: the row's own click wins (picks it).
        ctx.UpdatePointer(row, 195, 18, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(row, 195, 18, false, false, true, Vector2.Zero);
        Assert.Same(instrument, picked);
    }
}

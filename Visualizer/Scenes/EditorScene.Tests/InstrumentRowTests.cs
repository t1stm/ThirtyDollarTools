using EditorScene.Scenes.Components;
using OpenTK.Mathematics;
using Sundex.Components.Labels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class InstrumentRowTests
{
    private static (InstrumentRow row, Button edit, Button delete) NewRow(
        EditorTestContext ctx, Instrument instrument,
        Action<Instrument> onPick, Action<Instrument> onEdit, Action<Instrument> onDelete)
    {
        var row = new InstrumentRow(ctx, instrument, onPick, onEdit, onDelete) { Width = 300 };
        row.Layout();
        var buttons = row.Children.OfType<Button>().ToArray();
        return (row, buttons[0], buttons[1]);
    }

    [Fact]
    public void ClickingTheEditButton_FiresOnEdit_NotOnPickOrDelete()
    {
        var ctx = new EditorTestContext();
        var instrument = new Instrument { Name = "Layer" };
        Instrument? picked = null;
        Instrument? edited = null;
        Instrument? deleted = null;
        var (row, edit, _) = NewRow(ctx, instrument, i => picked = i, i => edited = i, i => deleted = i);

        var ex = edit.Computed.AbsoluteX + edit.Computed.Width / 2;
        var ey = edit.Computed.AbsoluteY + edit.Computed.Height / 2;
        ctx.UpdatePointer(row, ex, ey, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(row, ex, ey, false, false, true, Vector2.Zero);

        Assert.Same(instrument, edited);
        Assert.Null(picked);
        Assert.Null(deleted);
    }

    [Fact]
    public void ClickingTheDeleteButton_FiresOnDelete_NotOnPickOrEdit()
    {
        var ctx = new EditorTestContext();
        var instrument = new Instrument { Name = "Layer" };
        Instrument? picked = null;
        Instrument? edited = null;
        Instrument? deleted = null;
        var (row, _, delete) = NewRow(ctx, instrument, i => picked = i, i => edited = i, i => deleted = i);

        var dx = delete.Computed.AbsoluteX + delete.Computed.Width / 2;
        var dy = delete.Computed.AbsoluteY + delete.Computed.Height / 2;
        ctx.UpdatePointer(row, dx, dy, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(row, dx, dy, false, false, true, Vector2.Zero);

        Assert.Same(instrument, deleted);
        Assert.Null(picked);
        Assert.Null(edited);
    }

    [Fact]
    public void AVeryLongName_DoesNotPushTheButtonsOutOfTheRow()
    {
        var ctx = new EditorTestContext();
        var instrument = new Instrument { Name = new string('W', 200) };
        var (row, _, delete) = NewRow(ctx, instrument, _ => { }, _ => { }, _ => { });

        var right = row.Computed.AbsoluteX + row.Computed.Width;
        Assert.InRange(delete.Computed.AbsoluteX + delete.Computed.Width, 0, right);
    }

    [Fact]
    public void ClickingTheRowAwayFromTheButtons_FiresOnPick()
    {
        var ctx = new EditorTestContext();
        var instrument = new Instrument { Name = "Layer" };
        Instrument? picked = null;
        var (row, _, _) = NewRow(ctx, instrument, i => picked = i, _ => { }, _ => { });

        // Over the name label, clear of Edit/Delete which sit flush against the right edge.
        ctx.UpdatePointer(row, 20, 18, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(row, 20, 18, false, false, true, Vector2.Zero);

        Assert.Same(instrument, picked);
    }
}
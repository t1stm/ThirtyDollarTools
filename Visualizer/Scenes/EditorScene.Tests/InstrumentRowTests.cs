using OpenTK.Mathematics;
using Sundex.Components.Labels;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

public class InstrumentRowTests
{
    private static (InstrumentRow row, Button edit, Button delete) NewRow(
        EditorTestContext ctx, Instrument instrument,
        Action<Instrument> onPick, Action<Instrument> onEdit, Action<Instrument> onDelete,
        Action<Instrument>? onPreview = null, Action<Instrument>? onDuplicate = null)
    {
        var row = EditorTestContext.Styled(new InstrumentRow(ctx, instrument, [], onPick, onPreview ?? (_ => { }),
            onDuplicate ?? (_ => { }), onEdit, onDelete));
        row.Width = 428; // the selector's list: 460 less the frame's 2 x 16
        row.Layout();
        var buttons = row.Children.OfType<Button>().ToArray(); // Preview, Duplicate, Edit, Delete
        return (row, buttons[2], buttons[3]);
    }

    private static void Click(EditorTestContext ctx, InstrumentRow row, Button button)
    {
        var x = button.Computed.AbsoluteX + button.Computed.Width / 2;
        var y = button.Computed.AbsoluteY + button.Computed.Height / 2;
        ctx.UpdatePointer(row, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(row, x, y, false, false, true, Vector2.Zero);
    }

    [Fact]
    public void ClickingThePreviewButton_FiresOnPreview_NotOnPick()
    {
        var ctx = new EditorTestContext();
        var instrument = new Instrument { Name = "Layer" };
        Instrument? picked = null;
        Instrument? previewed = null;
        var (row, _, _) = NewRow(ctx, instrument, i => picked = i, _ => { }, _ => { }, i => previewed = i);

        Click(ctx, row, row.Children.OfType<Button>().First());

        Assert.Same(instrument, previewed);
        Assert.Null(picked);
    }

    [Fact]
    public void ClickingTheDuplicateButton_FiresOnDuplicate_NotOnPickOrEdit()
    {
        var ctx = new EditorTestContext();
        var instrument = new Instrument { Name = "Layer" };
        Instrument? picked = null;
        Instrument? edited = null;
        Instrument? duplicated = null;
        var (row, _, _) = NewRow(ctx, instrument, i => picked = i, i => edited = i, _ => { },
            onDuplicate: i => duplicated = i);

        Click(ctx, row, row.Children.OfType<Button>().ElementAt(1));

        Assert.Same(instrument, duplicated);
        Assert.Null(picked);
        Assert.Null(edited);
    }

    [Fact]
    public void ARow_ShowsItsSoundCount_AndSwapsItsFillWhenSelected()
    {
        var ctx = new EditorTestContext();
        var instrument = new Instrument { Name = "Layer" };
        instrument.AddSound("kick");
        var (row, _, _) = NewRow(ctx, instrument, _ => { }, _ => { }, _ => { });

        Assert.Contains("instrument-row-idle", row.Classes);
        row.Selected = true;
        Assert.Contains("instrument-row-selected", row.Classes);
        Assert.DoesNotContain("instrument-row-idle", row.Classes);
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
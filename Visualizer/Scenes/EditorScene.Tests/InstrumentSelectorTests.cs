using OpenTK.Mathematics;
using Sundex.Components.Labels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Dialogs;

namespace EditorScene.Tests;

public class InstrumentSelectorTests
{
    [Fact]
    public void Fill_ThenClickingARowsDeleteButton_FiresOnDelete()
    {
        var ctx = new EditorTestContext();
        var selector = EditorTestContext.Styled(new InstrumentSelector(ctx, ctx.NewAtlasStore()));
        var a = new Instrument { Name = "A" };
        selector.Fill([a]);
        selector.Layout();

        Instrument? deleted = null;
        selector.OnDelete = i => deleted = i;

        var list = selector.Children.OfType<ScrollView>().Single();
        var row = list.Children.OfType<InstrumentRow>().Single();
        var delete = row.Children.OfType<Button>().Last(); // Preview, Edit, then Delete
        var x = delete.Computed.AbsoluteX + delete.Computed.Width / 2;
        var y = delete.Computed.AbsoluteY + delete.Computed.Height / 2;
        ctx.UpdatePointer(selector, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(selector, x, y, false, false, true, Vector2.Zero);

        Assert.Same(a, deleted);
    }

    [Fact]
    public void ShiftA_MakesANewInstrument_PlainAOrCtrlShiftADoNot()
    {
        var ctx = new EditorTestContext();
        var selector = new InstrumentSelector(ctx, ctx.NewAtlasStore());
        var made = 0;
        selector.OnNew = () => made++;

        Assert.False(selector.HandleKeyDown(new OpenTK.Windowing.Common.KeyboardKeyEventArgs(
            OpenTK.Windowing.GraphicsLibraryFramework.Keys.A, 0, 0, false)));
        Assert.True(selector.HandleKeyDown(new OpenTK.Windowing.Common.KeyboardKeyEventArgs(
            OpenTK.Windowing.GraphicsLibraryFramework.Keys.A, 0, OpenTK.Windowing.GraphicsLibraryFramework.KeyModifiers.Shift, false)));
        Assert.False(selector.HandleKeyDown(new OpenTK.Windowing.Common.KeyboardKeyEventArgs(
            OpenTK.Windowing.GraphicsLibraryFramework.Keys.A, 0,
            OpenTK.Windowing.GraphicsLibraryFramework.KeyModifiers.Shift | OpenTK.Windowing.GraphicsLibraryFramework.KeyModifiers.Control, false)));

        Assert.Equal(1, made);
    }

    [Fact]
    public void Fill_ThenClickingARow_FiresOnPick()
    {
        var ctx = new EditorTestContext();
        var selector = EditorTestContext.Styled(new InstrumentSelector(ctx, ctx.NewAtlasStore()));
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
        var selector = EditorTestContext.Styled(new InstrumentSelector(ctx, ctx.NewAtlasStore()));
        selector.Fill([]);
        selector.Layout();

        var fired = 0;
        selector.OnNew = () => fired++;

        // The footer's button, shown even with no instruments.
        var newRow = ((Sundex.Components.Panels.Panel)selector.Children.Last()).Children.OfType<Button>().Single();
        var x = newRow.Computed.AbsoluteX + newRow.Computed.Width / 2;
        var y = newRow.Computed.AbsoluteY + newRow.Computed.Height / 2;
        ctx.UpdatePointer(selector, x, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(selector, x, y, false, false, true, Vector2.Zero);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void Fill_MarksTheGivenInstrument_AndNoOther()
    {
        var ctx = new EditorTestContext();
        var selector = EditorTestContext.Styled(new InstrumentSelector(ctx, ctx.NewAtlasStore()));
        var a = new Instrument { Name = "A" };
        var b = new Instrument { Name = "B" };

        selector.Fill([a, b], b);

        var rows = selector.Children.OfType<ScrollView>().Single().Children.OfType<InstrumentRow>().ToArray();
        Assert.Contains("instrument-row-idle", rows[0].Classes);
        Assert.Contains("instrument-row-selected", rows[1].Classes);
    }

    [Fact]
    public void Fill_Again_ReplacesTheRows()
    {
        var ctx = new EditorTestContext();
        var selector = EditorTestContext.Styled(new InstrumentSelector(ctx, ctx.NewAtlasStore()));
        selector.Fill([new Instrument { Name = "A" }, new Instrument { Name = "B" }]);

        selector.Fill([new Instrument { Name = "C" }]);

        var row = Assert.Single(selector.Children.OfType<ScrollView>().Single().Children.OfType<InstrumentRow>());
        Assert.Equal("C", row.Instrument.Name);
    }

    [Fact]
    public void Fill_WithNoInstruments_ShowsTheEmptyStateInsteadOfTheList()
    {
        var ctx = new EditorTestContext();
        var selector = EditorTestContext.Styled(new InstrumentSelector(ctx, ctx.NewAtlasStore()));

        selector.Fill([]);
        Assert.Empty(selector.Children.OfType<ScrollView>());

        selector.Fill([new Instrument { Name = "A" }]);
        Assert.Single(selector.Children.OfType<ScrollView>());
        Assert.Equal(3, selector.Children.Count);
    }
}

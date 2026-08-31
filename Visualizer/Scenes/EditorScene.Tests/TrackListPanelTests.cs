using EditorScene.State;
using OpenTK.Mathematics;
using EditorScene.Scenes.Layout;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class TrackListPanelTests
{
    [Fact]
    public void DraggingARowsBlipOntoAnotherRow_ReordersTheTrack_AndTheOrderIsSaved()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var first = state.AddTrack();
        var second = state.AddTrack();

        // Any color: the blip only has to exist to be a handle.
        var list = EditorTestContext.Styled(new TrackListPanel(ctx, state) { TrackColor = _ => Vector4.One });
        list.Width = 200;
        list.Height = 400;
        list.Rebuild();
        list.Layout();

        var rows = list.Children.OfType<EditorTrack>().ToArray();
        var handle = rows[1].DragHandle!;
        var (grabX, grabY) = (handle.Computed.AbsoluteX + 2, handle.Computed.AbsoluteY + 2);
        var dropY = rows[0].Computed.AbsoluteY + rows[0].Computed.Height / 2;

        ctx.UpdatePointer(list, grabX, grabY, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(list, grabX, dropY, true, false, false, Vector2.Zero);
        ctx.UpdatePointer(list, grabX, dropY, false, false, true, Vector2.Zero);

        Assert.Equal([second, first], state.Project.Tracks);
        Assert.Equal([second.Name, first.Name],
            ProjectFile.Load(ProjectFile.Save(state.Project)).Tracks.Select(track => track.Name));

        state.Undo(); // one entry for the whole drag
        Assert.Equal([first, second], state.Project.Tracks);
    }

    [Fact]
    public void CtrlClickingRows_ThenDraggingTheBlip_MovesTheWholeSelectionInListOrder()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var (a, b, c, d) = (state.AddTrack(), state.AddTrack(), state.AddTrack(), state.AddTrack());

        var list = EditorTestContext.Styled(new TrackListPanel(ctx, state) { TrackColor = _ => Vector4.One });
        list.Width = 200;
        list.Height = 400;
        list.Rebuild();
        list.Layout();
        var rows = list.Children.OfType<EditorTrack>().ToArray();

        Click(ctx, list, rows[0]); // A
        list.CtrlHeld = true;
        Click(ctx, list, rows[2]); // + C
        Assert.Equal([a, c], state.SelectedTracks);
        Assert.Null(state.SelectedTrack); // multi-selection: the single-selection consumers go quiet

        // Grab C's blip and drop the pair on D, the last row.
        var handle = rows[2].DragHandle!;
        var dropY = rows[3].Computed.AbsoluteY + rows[3].Computed.Height / 2;
        ctx.UpdatePointer(list, handle.Computed.AbsoluteX + 2, handle.Computed.AbsoluteY + 2,
            true, true, false, Vector2.Zero);
        ctx.UpdatePointer(list, handle.Computed.AbsoluteX + 2, dropY, true, false, false, Vector2.Zero);
        ctx.UpdatePointer(list, handle.Computed.AbsoluteX + 2, dropY, false, false, true, Vector2.Zero);

        // A stays above C: the block keeps the list order, not the click order.
        Assert.Equal([b, d, a, c], state.Project.Tracks);
        Assert.Equal([b.Name, d.Name, a.Name, c.Name],
            ProjectFile.Load(ProjectFile.Save(state.Project)).Tracks.Select(track => track.Name));

        state.Undo();
        Assert.Equal([a, b, c, d], state.Project.Tracks);
    }

    /// <summary>A press+release on the row's empty right end - past the name, short of the × button.</summary>
    private static void Click(EditorTestContext ctx, TrackListPanel list, EditorTrack row)
    {
        var y = row.Computed.AbsoluteY + row.Computed.Height / 2;
        ctx.UpdatePointer(list, row.Computed.AbsoluteX + row.Computed.Width - 5, y, true, true, false, Vector2.Zero);
        ctx.UpdatePointer(list, row.Computed.AbsoluteX + row.Computed.Width - 5, y, false, false, true, Vector2.Zero);
    }
}

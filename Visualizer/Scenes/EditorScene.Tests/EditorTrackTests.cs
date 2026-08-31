using EditorScene.State;
using OpenTK.Mathematics;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Layout;

namespace EditorScene.Tests;

public class EditorTrackTests
{
    [Fact]
    public void DoubleClickOnARow_OpensTheTrack()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        // Styled first, then sized - the sheet gives the row a percent width, and a test
        // that needs a concrete one has to set it after, exactly as EditorInterface.Resize does.
        var row = EditorTestContext.Styled(new EditorTrack(ctx, track, state));
        row.Width = 200;
        row.Layout();

        // Two clicks on the row's empty right end (past the name and × button).
        for (var i = 0; i < 2; i++)
        {
            ctx.UpdatePointer(row, 195, 30, true, true, false, Vector2.Zero);
            ctx.UpdatePointer(row, 195, 30, false, false, true, Vector2.Zero);
        }

        Assert.Same(track, state.OpenedTrack);
        Assert.Equal([track], state.Project.Tracks); // the × button was not hit
    }

    [Fact]
    public void RightClickOnARow_FiresOnContextMenu_WithThatTrackAndTheCursor()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        ProjectTrack? seen = null;
        (float x, float y) at = default;
        var row = EditorTestContext.Styled(new EditorTrack(ctx, track, state));
        row.Width = 200;
        row.OnContextMenu = (t, x, y) =>
        {
            seen = t;
            at = (x, y);
        };
        row.Layout();

        ctx.UpdatePointer(row, 195, 30, true, false, false, Vector2.Zero, true);

        Assert.Same(track, seen);
        Assert.Equal((195, 30), at); // the menu hangs off the cursor, not the row
    }
}
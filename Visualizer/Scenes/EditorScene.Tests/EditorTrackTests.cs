using EditorScene.Scenes.Components;
using OpenTK.Mathematics;

namespace EditorScene.Tests;

public class EditorTrackTests
{
    [Fact]
    public void DoubleClickOnARow_OpensTheTrack()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        var row = new EditorTrack(ctx, track, state) { Width = 200 };
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
}

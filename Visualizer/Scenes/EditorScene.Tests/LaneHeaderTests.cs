using EditorScene.State;
using OpenTK.Mathematics;
using EditorScene.Scenes.Views;

namespace EditorScene.Tests;

public class LaneHeaderTests
{
    private static (EditorState state, ArrangementView arrangement, LaneHeader header) NewHeader(float height)
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var arrangement = new ArrangementView(ctx, state) { Width = 800, Height = height };
        var header = new LaneHeader(ctx, state, arrangement) { Width = LaneHeader.GutterWidth, Height = height };
        arrangement.Layout();
        return (state, arrangement, header);
    }

    [Fact]
    public void ALaneFarPastTheOldTwentyFourChannelCap_StillGetsMuteAndSoloButtons()
    {
        // A clip's block is laid out straight from placement.Channel, uncapped (see
        // ArrangementView.DoLayout), so ArrangementView.ChannelCount and the LaneLinePool
        // shared by the divider lines and this header's M/S toggles have to reach as deep:
        // a lane past any fixed pool size still gets its line and its buttons.
        const int channel = 40;
        var height = ArrangementView.RulerHeight + (channel + 2) * ArrangementView.LaneHeight;
        var (state, arrangement, header) = NewHeader(height);
        var track = state.AddTrack();
        state.PlaceTrack(track, channel, 0);
        arrangement.Refresh();
        header.RefreshChannels();
        arrangement.Layout();
        header.Layout();

        Assert.True(arrangement.Channels > channel);
        var (mute, solo) = header.Rows[channel];
        Assert.True(mute.Visible);
        Assert.True(solo.Visible);
    }

    [Fact]
    public void ScrollingTheArrangement_MovesTheHeaderButtons_ToStayAligned()
    {
        // Mirrors EditorInterface's OnScrolled wiring: the header has no other way to
        // learn the arrangement scrolled (it isn't reachable from any project/channel
        // event), so without this, its M/S toggles would freeze in place while the
        // lanes scrolled underneath them.
        const int channel = 15; // 17 lanes (15 + 2 spare): 748px, taller than the 382px grid
        var (state, arrangement, header) = NewHeader(400);
        arrangement.OnScrolled = header.InvalidateLayout;
        var track = state.AddTrack();
        state.PlaceTrack(track, channel, 0);
        arrangement.Refresh();
        header.RefreshChannels();
        arrangement.Layout();
        header.Layout();

        var yBefore = header.Rows[0].Mute.Computed.Y;

        arrangement.HandleScroll(new Vector2(0, -1));
        arrangement.Layout();
        header.Layout();

        Assert.True(arrangement.ScrollY > 0);
        Assert.Equal(yBefore - arrangement.ScrollY, header.Rows[0].Mute.Computed.Y, 3);
    }

    [Fact]
    public void ScrollingALaneIntoView_QueuesItsButtonsForRender()
    {
        // A row hidden at the initial draw (below the grid) has to get its DrawSelf once
        // scrolling flips Visible back on: its M/S toggles are hit-testable either way, so
        // without it they would mute the lane while never being painted.
        const int channel = 15;
        var (state, arrangement, header) = NewHeader(400);
        arrangement.OnScrolled = header.InvalidateLayout;
        var track = state.AddTrack();
        state.PlaceTrack(track, channel, 0);
        arrangement.Refresh();
        header.RefreshChannels();
        arrangement.Layout();
        header.DrawTo(arrangement.Context);

        var (mute, _) = header.Rows[channel];
        Assert.False(mute.Visible); // starts below the 400px viewport

        for (var i = 0; i < 100 && !mute.Visible; i++)
        {
            arrangement.HandleScroll(new Vector2(0, -1));
            arrangement.Layout();
            header.Layout();
        }

        Assert.True(mute.Visible);
        Assert.True(mute.Drawn);
    }

    [Fact]
    public void ARowStraddlingAnEdge_IsClippedToTheLaneStrip()
    {
        // The gutter spans the arrangement's full height and Visible only culls whole rows,
        // so the last partly-visible row needs a clip rect: without one its M/S buttons hang
        // past the panel's bottom edge and paint over the hint bar below the grid area.
        var (state, arrangement, header) = NewHeader(400);
        var track = state.AddTrack();
        state.PlaceTrack(track, 15, 0); // 17 lanes: taller than the 400px gutter
        arrangement.Refresh();
        arrangement.Layout();
        header.Layout();

        Assert.NotNull(header.RowClip);
        var clip = header.RowClip.Value;
        Assert.Equal((int)ArrangementView.RulerHeight, clip.Y); // starts below the ruler band
        Assert.Equal(400, clip.W); // never past the gutter's own bottom

        // The clip has to be doing the work: the bottom-most shown row really does overflow.
        var straddling = header.Rows.Last(row => row.Mute.Visible).Mute;
        Assert.True(straddling.Computed.AbsoluteY + straddling.Computed.Height > clip.W);
    }

    [Fact]
    public void ImportingATrack_RecomputesAndQueuesTheNewLanesButtons()
    {
        // Mirrors EditorInterface.RefreshProject: a project change (import/load/undo/...)
        // must both InvalidateLayout and DrawTo the header, or a lane that only exists
        // because of the change never gets Visible recomputed/queued for render (the
        // header otherwise only relayouts from ArrangementView.OnScrolled).
        const int channel = 40;
        var height = ArrangementView.RulerHeight + (channel + 2) * ArrangementView.LaneHeight;
        var (state, arrangement, header) = NewHeader(height);
        arrangement.Layout();
        header.Layout();

        var track = state.AddTrack();
        state.PlaceTrack(track, channel, 0);
        arrangement.Refresh(); // what RefreshProject actually calls

        header.InvalidateLayout();
        header.DrawTo(arrangement.Context);

        var (mute, solo) = header.Rows[channel];
        Assert.True(mute.Visible);
        Assert.True(solo.Visible);
    }
}
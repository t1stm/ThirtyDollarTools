using EditorScene.Scenes.Components;
using OpenTK.Mathematics;

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
        // Regression: LaneLinePool (shared by the grid's divider lines and this header's
        // M/S toggle pool) used to be 24 - ArrangementView.ChannelCount's clamp silently
        // capped the reported lane count there, so a clip on a deeper channel still got
        // its block (laid out straight from placement.Channel, uncapped - see
        // ArrangementView.DoLayout) but no divider line or M/S buttons.
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
        // Regression: a row hidden at the initial draw (below the grid) never got its
        // DrawSelf once scrolling flipped Visible back on, so its M/S toggles were
        // hit-testable - clicking them muted the lane - but never painted.
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
using EditorScene.Scenes.Components;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

// Drives UIContext.UpdatePointer directly with primitives, like Sundex's InputRoutingTests.
public class ArrangementViewTests
{
    private static void Press(UIContext ctx, UIElement root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, true, true, false, Vector2.Zero);
    }

    private static void Drag(UIContext ctx, UIElement root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, true, false, false, Vector2.Zero);
    }

    private static void Release(UIContext ctx, UIElement root, float x, float y)
    {
        ctx.UpdatePointer(root, x, y, false, false, true, Vector2.Zero);
    }

    private static (EditorTestContext ctx, EditorState state, ArrangementView view) NewView()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var view = new ArrangementView(ctx, state) { Width = 800, Height = 400 };
        state.OnProjectChanged += view.Refresh; // the EditorInterface wiring
        state.OnPlacementSelectionChanged += _ => view.RefreshSelection();
        view.Layout();
        return (ctx, state, view);
    }

    [Fact]
    public void PressOnAClip_SelectsIt_AndDragMovesItWithSnap()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack(); // default pattern: one 4/4 bar = 4 quarter notes
        var placement = state.PlaceTrack(track, 1, 4);
        view.Layout();

        // ppq 24, lane height 44, ruler 18: the clip spans x 96..192 on lane 1 (y 62..106).
        Press(ctx, view, 100, 78);
        Assert.Same(placement, state.SelectedPlacement);
        Assert.Same(track, state.SelectedTrack);

        // One beat right, one lane down; the 4 px grab offset must not shift the snap.
        Drag(ctx, view, 124, 122);
        Assert.Equal(5, placement.StartQuarterNotes);
        Assert.Equal(2, placement.Channel);

        Release(ctx, view, 124, 122);
        view.Update(ctx); // drag-end housekeeping (deferred rebuild)

        // The clip is still draggable after the rebuild: its block was recreated.
        Press(ctx, view, 100 + 24, 78 + 44);
        Assert.Same(placement, state.SelectedPlacement);
    }

    [Fact]
    public void ClickOnAnEmptyLane_PlacesTheSelectedPattern()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        state.SelectTrack(track);
        view.Layout();

        Press(ctx, view, 98, ArrangementView.RulerHeight + 3 * ArrangementView.LaneHeight + 5);
        Release(ctx, view, 98, ArrangementView.RulerHeight + 3 * ArrangementView.LaneHeight + 5);

        var placement = Assert.Single(state.Project.Placements);
        Assert.Same(track, placement.Track);
        Assert.Equal(3, placement.Channel);
        Assert.Equal(4, placement.StartQuarterNotes); // 98 px / 24 ppq, snapped to the beat
        Assert.Same(placement, state.SelectedPlacement);
    }

    [Fact]
    public void ClickingEmptySpace_WithNothingToPlace_JustDeselects()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 0, 0);
        state.SelectPlacement(placement);
        view.Layout();

        Press(ctx, view, 700, 300);
        Release(ctx, view, 700, 300);

        Assert.Null(state.SelectedPlacement);
        Assert.Equal([placement], state.Project.Placements); // nothing new was placed
    }

    [Fact]
    public void DeleteKey_RemovesTheSelectedClip()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        state.PlaceTrack(track, 1, 0);
        view.Layout();

        Press(ctx, view, 10, 68); // selects the clip and focuses the view
        Release(ctx, view, 10, 68);
        Assert.Same(view, ctx.FocusedElement);

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Delete, 0, 0, false));

        Assert.Empty(state.Project.Placements);
        Assert.Null(state.SelectedPlacement);
        Assert.Equal([track], state.Project.Tracks); // the pattern itself survives
    }

    [Fact]
    public void CtrlWheel_ZoomsInAnchoredAtThePointer()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        state.SelectTrack(track);
        view.Layout();
        view.WheelZooms = true;

        // Wheel up at x=200 — beat 8.33 at the default 24 px per quarter.
        ctx.UpdatePointer(view, 200, 100, false, false, false, new Vector2(0, 1));
        Assert.True(view.PixelsPerQuarter > 24f);

        // The beat under the cursor didn't move: clicking there still places at
        // beat 8. (Unanchored zoom would leave beat ~7.25 under the pointer.)
        Press(ctx, view, 200, 100);
        Release(ctx, view, 200, 100);
        var placement = Assert.Single(state.Project.Placements);
        Assert.Equal(8, placement.StartQuarterNotes);
    }

    [Fact]
    public void PlainWheel_StillPans()
    {
        var (ctx, state, view) = NewView();
        state.SelectTrack(state.AddTrack());
        view.Layout();

        // Wheel down pans right by 48 px = 2 beats; a click at x=0 now places at beat 2.
        ctx.UpdatePointer(view, 400, 100, false, false, false, new Vector2(0, -1));
        Assert.Equal(24f, view.PixelsPerQuarter);

        Press(ctx, view, 0, 100);
        Release(ctx, view, 0, 100);
        var placement = Assert.Single(state.Project.Placements);
        Assert.Equal(2, placement.StartQuarterNotes);
    }

    [Fact]
    public void DoubleClickOnAClip_FiresOnOpenTrack()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        state.PlaceTrack(track, 0, 0);
        view.Layout();

        ProjectTrack? opened = null;
        view.OnOpenTrack = t => opened = t;

        Press(ctx, view, 10, 28);
        Release(ctx, view, 10, 28);
        Press(ctx, view, 10, 28); // within the double-click window
        Release(ctx, view, 10, 28);

        Assert.Same(track, opened);
    }

    [Fact]
    public void DoubleClick_SwappingTheViewMidDispatch_DoesNotStrandThePointerCapture()
    {
        // The EditorInterface flow: double-press on a clip opens the track, which
        // removes the arrangement from the grid area mid-dispatch. The capture on
        // the (now detached) clip block must be released, or every later pointer
        // update early-returns and the whole UI stops responding to the mouse.
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var gridArea = new Sundex.Components.Panels.Panel(ctx) { Width = 800, Height = 400 };
        var view = new ArrangementView(ctx, state) { Width = 800, Height = 400 };
        var editor = new TrackEditorView(ctx, state) { Width = 800, Height = 400 };
        gridArea.AddChild(view);
        state.OnProjectChanged += view.Refresh;
        view.OnOpenTrack = t =>
        {
            state.OpenTrack(t);
            gridArea.RemoveChild(view);
            gridArea.AddChild(editor);
        };
        var track = state.AddTrack();
        state.PlaceTrack(track, 0, 0);
        gridArea.Layout();

        Press(ctx, gridArea, 10, 28);
        Release(ctx, gridArea, 10, 28);
        Press(ctx, gridArea, 10, 28); // opens the track, swapping views mid-dispatch
        Release(ctx, gridArea, 10, 28);

        Assert.Same(track, state.OpenedTrack);
        Assert.Null(ctx.CapturedElement);

        // The pointer reaches the note editor that replaced the arrangement.
        gridArea.Layout(); // the app lays out every frame
        Press(ctx, gridArea, 400, 200);
        Release(ctx, gridArea, 400, 200);
        var hit = ctx.HoverTarget;
        while (hit != null && hit != editor) hit = hit.Parent;
        Assert.Same(editor, hit);
    }

    [Fact]
    public void DoubleClick_StillFires_WhenAFrameUpdateRunsBetweenTheClicks()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        state.PlaceTrack(track, 0, 0);
        view.Layout();

        ProjectTrack? opened = null;
        view.OnOpenTrack = t => opened = t;

        // The app runs Update every frame. A plain click must not rebuild the clip
        // block, or the second press lands on a fresh element and UIContext's
        // same-element double-press check never fires.
        Press(ctx, view, 10, 28);
        Release(ctx, view, 10, 28);
        view.Update(ctx);
        Press(ctx, view, 10, 28);
        Release(ctx, view, 10, 28);

        Assert.Same(track, opened);
    }
}

using EditorScene.Scenes.Components;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
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

    private static void Click(UIContext ctx, UIElement root, float x, float y)
    {
        Press(ctx, root, x, y);
        Release(ctx, root, x, y);
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

        // Wheel up at x=200 - beat 8.33 at the default 24 px per quarter.
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
    public void ShiftWheel_PansTimeInstead()
    {
        // Same binding as the note editor: Shift+wheel pans time (FineSnap = Shift held).
        var (ctx, state, view) = NewView();
        state.SelectTrack(state.AddTrack());
        view.Layout();
        view.FineSnap = true;

        // Wheel down pans right by 48 px = 2 beats; a click at x=0 now places at beat 2.
        ctx.UpdatePointer(view, 400, 100, false, false, false, new Vector2(0, -1));
        Assert.Equal(24f, view.PixelsPerQuarter);
        Assert.Equal(0f, view.ScrollY); // time only - the lanes never moved

        view.FineSnap = false;
        Press(ctx, view, 0, 100);
        Release(ctx, view, 0, 100);
        var placement = Assert.Single(state.Project.Placements);
        Assert.Equal(2, placement.StartQuarterNotes);
    }

    [Fact]
    public void PlainWheel_ScrollsLanesVertically_NeverTime()
    {
        var (ctx, state, view) = NewView(); // 800x400: gridHeight 382, ~8 lanes fit
        var track = state.AddTrack();
        state.PlaceTrack(track, 10, 0); // forces 12 lanes (10 + 2 spare) - taller than the grid
        view.Layout();

        ctx.UpdatePointer(view, 400, 100, false, false, false, new Vector2(0, -1));
        Assert.True(view.ScrollY > 0);
        Assert.Equal(24f, view.PixelsPerQuarter); // time untouched

        Press(ctx, view, 0, 100);
        Release(ctx, view, 0, 100);
        Assert.Equal(0, state.Project.Placements[^1].StartQuarterNotes); // time untouched
    }

    [Fact]
    public void VerticalScroll_ClampsToTheContentRange()
    {
        var (ctx, state, view) = NewView(); // 800x400: gridHeight 382
        var track = state.AddTrack();
        state.PlaceTrack(track, 10, 0); // 12 lanes: max scroll = 12*44 - 382 = 146
        view.Layout();

        for (var i = 0; i < 8; i++)
            ctx.UpdatePointer(view, 400, 100, false, false, false, new Vector2(0, -1));

        Assert.Equal(146f, view.ScrollY, 3);
    }

    [Fact]
    public void RemovingATrack_ThatShrinksTheLaneCount_DoesNotAutoScroll()
    {
        // Regression: eagerly re-clamping ScrollY to the new (shrunk) MaxScrollY on every
        // layout auto-scrolled the view under a still-held pointer whenever a removal
        // shrank the lane count - since right-press is level-triggered, whatever slid
        // under the cursor next got deleted too, cascading through every track that had
        // been off-screen below it.
        var (ctx, state, view) = NewView(); // 800x400: gridHeight 382
        var track = state.AddTrack();
        var deep = state.PlaceTrack(track, 10, 0); // 12 lanes: max scroll = 12*44 - 382 = 146
        view.Layout();

        for (var i = 0; i < 8; i++)
            ctx.UpdatePointer(view, 400, 100, false, false, false, new Vector2(0, -1));
        Assert.Equal(146f, view.ScrollY, 3);

        state.RemovePlacement(deep); // channel count shrinks back down to MinChannels (8)
        view.Layout();

        Assert.Equal(146f, view.ScrollY, 3); // stayed put - not auto-scrolled back up
    }

    [Fact]
    public void ScrollingPastTheRuler_ClipsKeepMovingSmoothly_NeverPinnedAtTheTop()
    {
        // Regression: a clip's block.Y used to clamp to RulerHeight once scrolled above
        // it, pinning every clip that scrolled past that band to the same Y - freezing
        // its label there instead of sliding smoothly off past the ruler (now masked by
        // the ruler's paint order instead of a geometric clamp, see Refresh).
        var (ctx, state, view) = NewView(); // 800x400
        var track = state.AddTrack();
        var top = state.PlaceTrack(track, 0, 0); // the clip under test
        state.PlaceTrack(track, 10, 0); // forces enough lanes to actually need scrolling
        view.Layout();

        for (var i = 0; i < 8; i++)
            ctx.UpdatePointer(view, 400, 100, false, false, false, new Vector2(0, -1));
        view.Layout();

        var block = view.Blocks.Single(b => b.Placement == top);
        Assert.True(block.Computed.Y < 0); // scrolled fully past the ruler, not pinned at 18 (or 0)
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
        var gridArea = new Panel(ctx) { Width = 800, Height = 400 };
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

    [Fact]
    public void SelectTool_MarqueeDrag_SelectsIntersectingClips_AndNeverPlaces()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack(); // default pattern: one 4/4 bar = 4 quarter notes
        var inside = state.PlaceTrack(track, 1, 4); // spans quarters 4..8, lane 1 (y 62..106)
        var outside = state.PlaceTrack(track, 5, 40);
        state.SelectTrack(track);
        view.Layout();
        state.ActiveTool = EditorTool.Select;

        // Box from (quarter 3, lane 0.5) to (quarter 9, lane 2): intersects `inside`'s span/lane.
        Press(ctx, view, 3 * 24, 18 + 22);
        Drag(ctx, view, 9 * 24, 18 + 88);
        Release(ctx, view, 9 * 24, 18 + 88);
        view.Update(ctx); // marquee commit runs on capture loss

        Assert.Equal([inside], state.SelectedPlacements);
        Assert.DoesNotContain(outside, state.SelectedPlacements);
        Assert.Equal(2, state.Project.Placements.Count); // the Select tool never places
    }

    [Fact]
    public void SelectTool_Marquee_SelectsAClip_EvenWhenTheBoxNeverReachesItsTopEdge()
    {
        // Regression: a lane is a whole rendered row (Channel .. Channel+1), not a
        // point. A box whose channel range never reaches the row's top edge must
        // still select an intersecting clip (the time axis already used proper span
        // intersection; the channel axis didn't).
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 1, 4); // spans quarters 4..8, lane 1 (y 62..106)
        view.Layout();
        state.ActiveTool = EditorTool.Select;

        // Anchor at (quarter 6, lane 3) - an empty lane, off the clip's own block
        // entirely, so the press starts a marquee instead of grabbing the clip
        // directly. Cursor at (quarter 6, lane 1.5) - inside the clip's row, short of
        // its top edge (minChannel=1.5 > 1).
        Press(ctx, view, 6 * 24, 18 + 3 * 44);
        Drag(ctx, view, 6 * 24, 18 + 1.5f * 44);
        Release(ctx, view, 6 * 24, 18 + 1.5f * 44);
        view.Update(ctx);

        Assert.Equal([placement], state.SelectedPlacements);
    }

    [Fact]
    public void SelectTool_CtrlMarquee_AppendsWithoutTouchingTheExistingSelection()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var already = state.PlaceTrack(track, 0, 0);
        var toAdd = state.PlaceTrack(track, 1, 4);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetPlacementSelection([already]);
        view.WheelZooms = true; // Ctrl held

        // Anchor past `already`'s own clip (quarters 0..4) on its row, so the press
        // lands on empty canvas instead of hitting that ClipBlock directly.
        Press(ctx, view, 100, 18 + 22);
        Drag(ctx, view, 9 * 24, 18 + 88);
        Release(ctx, view, 9 * 24, 18 + 88);
        view.Update(ctx);

        Assert.Equal([already, toAdd], state.SelectedPlacements);
    }

    [Fact]
    public void SelectTool_ShiftMarquee_RemovesIntersectingClips()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var a = state.PlaceTrack(track, 1, 4);
        var b = state.PlaceTrack(track, 5, 40);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetPlacementSelection([a, b]);
        view.FineSnap = true; // Shift held

        Press(ctx, view, 3 * 24, 18 + 22);
        Drag(ctx, view, 9 * 24, 18 + 88);
        Release(ctx, view, 9 * 24, 18 + 88);
        view.Update(ctx);

        Assert.Equal([b], state.SelectedPlacements);
    }

    [Fact]
    public void SelectTool_EmptyMarquee_ClearsTheSelection()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 1, 4);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetPlacementSelection([placement]);

        // A box far from any clip.
        Press(ctx, view, 700, 300);
        Drag(ctx, view, 750, 320);
        Release(ctx, view, 750, 320);
        view.Update(ctx);

        Assert.Empty(state.SelectedPlacements);
    }

    [Fact]
    public void SelectTool_ClickOnAClip_ReplacesSelection_WithoutStartingADrag()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 1, 4);
        view.Layout();
        state.ActiveTool = EditorTool.Select;

        Press(ctx, view, 100, 78); // the clip's cell (see PressOnAClip_..)
        Drag(ctx, view, 124, 122); // would move it under the Draw tool
        Release(ctx, view, 124, 122);

        Assert.Equal([placement], state.SelectedPlacements);
        Assert.Equal(4, placement.StartQuarterNotes); // never moved
        Assert.Equal(1, placement.Channel);
    }

    [Fact]
    public void SelectTool_CtrlClickOnAClip_AppendsWithoutToggling()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var a = state.PlaceTrack(track, 1, 4);
        var b = state.PlaceTrack(track, 2, 20);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetPlacementSelection([a]);
        view.WheelZooms = true; // Ctrl held

        Click(ctx, view, 100, 78); // `a` again: already selected, append is a no-op, not a toggle
        Assert.Equal([a], state.SelectedPlacements);

        Click(ctx, view, (float)(20 * 24) + 4, 18 + 2 * 44 + 5); // `b`'s cell
        Assert.Equal([a, b], state.SelectedPlacements);
    }

    [Fact]
    public void DeleteKey_RemovesTheWholeMultiSelection_AsOneUndoEntry()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var a = state.PlaceTrack(track, 1, 4);
        var b = state.PlaceTrack(track, 2, 20);
        view.Layout();
        Press(ctx, view, 700, 300); // focuses the view without hitting either clip
        Release(ctx, view, 700, 300);
        state.SetPlacementSelection([a, b]);

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Delete, 0, 0, false));

        Assert.Empty(state.Project.Placements);
        state.Undo(); // one Ctrl+Z restores both
        Assert.Equal([a, b], state.Project.Placements);
    }

    [Fact]
    public void CtrlA_SelectsEveryPlacement()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var a = state.PlaceTrack(track, 0, 0);
        var b = state.PlaceTrack(track, 1, 4);
        view.Layout();
        Press(ctx, view, 700, 300); // focuses the view
        Release(ctx, view, 700, 300);

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.A, 0, KeyModifiers.Control, false));

        Assert.Equal([a, b], state.SelectedPlacements);
    }

    [Fact]
    public void CtrlCV_CopiesAndPastesTheSelection_ThroughTheFocusedView()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 1, 4);
        view.Layout();
        Press(ctx, view, 700, 300); // focuses the view
        Release(ctx, view, 700, 300);
        state.SetPlacementSelection([placement]);

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.C, 0, KeyModifiers.Control, false));
        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.V, 0, KeyModifiers.Control, false));

        Assert.Equal(2, state.Project.Placements.Count);
        Assert.Equal([placement, state.SelectedPlacement!], state.Project.Placements);
    }
}
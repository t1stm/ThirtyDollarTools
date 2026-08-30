using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Labels;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Views;

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
        // Styled like the editor styles it: the grid's colors are settings on the view
        // (class arrangement-canvas), so an unstyled one paints everything transparent.
        var view = EditorTestContext.Styled(new ArrangementView(ctx, state) { Width = 800, Height = 400 });
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
        // Layout must not re-clamp ScrollY to a shrunk MaxScrollY: the view moving under a
        // still-held pointer would slide the next lane under the cursor, and right-press is
        // level-triggered, so that one gets deleted too.
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
        // A clip's block.Y is not clamped to RulerHeight: clips scrolled above the ruler keep
        // moving with the scroll and are hidden by the ruler's paint order (see Refresh),
        // rather than being pinned at the band's edge.
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
        // A lane is a whole rendered row (Channel .. Channel+1), not a point, so the channel
        // axis intersects by span like the time axis: a box that never reaches the row's top
        // edge still selects a clip it overlaps.
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

    [Fact]
    public void AClipBlock_IsClippedToTheLanesBelowTheRuler()
    {
        // Refresh re-adds the ruler after the clip blocks so it paints over them, but that
        // only orders siblings inside one Index layer. A ClipBlock's track name Label sits a
        // layer deeper, and deeper layers render last, so the names need a clip rect of their
        // own to stay out of the ruler band.
        var (_, state, view) = NewView();
        var track = state.AddTrack();
        state.PlaceTrack(track, 0, 0);
        view.Layout();

        // Only the name needs it: the clip's own fill is batched with the grid lines, under
        // the ruler by layer. The names are a batch of their own, queued past every child.
        var clip = view.ClipLabels.ClipRect;
        Assert.NotNull(clip);
        Assert.Equal((int)ArrangementView.RulerHeight, clip.Value.Y); // ruler band excluded
        Assert.Equal(400, clip.Value.W); // and never past the view's own bottom
    }

    [Fact]
    public void AClipName_IsClippedToItsOwnClipsBox_AndReclipsAsTheZoomMovesIt()
    {
        // The batch's scissor bounds the whole pool at once, so it cannot stop one name
        // running over the clip beside it - that needs the per-slot box, which has to
        // follow its clip every time the zoom or the scroll moves one.
        var (_, state, view) = NewView();
        var track = state.AddTrack();
        state.PlaceTrack(track, 0, 0);
        view.Layout();

        AssertBoxedInItsClip();

        view.PixelsPerQuarter *= 4;
        view.Layout();

        AssertBoxedInItsClip();
        return;

        void AssertBoxedInItsClip()
        {
            var slot = view.ClipLabels.Slots[0];
            var box = view.Blocks[0].Computed;
            var padding = slot.X - box.AbsoluteX; // ClipPadding, the view's own private business

            Assert.NotEqual(Vector4.Zero, slot.Clip); // all-zero reads as "unclipped" in the shader
            Assert.Equal(new Vector4(box.AbsoluteX + padding, box.AbsoluteY,
                box.AbsoluteX + box.Width - padding, box.AbsoluteY + box.Height), slot.Clip);
        }
    }

    [Fact]
    public void RecoloringATrack_RepaintsItsClips_FromThePalette()
    {
        var (_, state, view) = NewView();
        var track = state.AddTrack();
        state.PlaceTrack(track, 0, 0);
        view.Layout();

        Assert.Equal(view.ClipColor, view.FillOf(view.Blocks[0]));

        // The state change refreshes the view through the wiring NewView set up.
        state.SetTrackColor(track, 2);
        view.Layout();

        Assert.NotEmpty(view.ClipPalette);
        Assert.Equal(view.ClipPalette[2], view.FillOf(view.Blocks[0]));

        // Past the palette's end wraps rather than throwing - a retuned palette must not
        // break a project colored against a longer one.
        state.SetTrackColor(track, view.ClipPalette.Length + 1);
        view.Layout();
        Assert.Equal(view.ClipPalette[1], view.FillOf(view.Blocks[0]));

        state.SetTrackColor(track, null);
        view.Layout();
        Assert.Equal(view.ClipColor, view.FillOf(view.Blocks[0]));
    }

    [Fact]
    public void SelectingAClip_LiftsItsOwnColor_RatherThanReplacingIt()
    {
        var (ctx, state, view) = NewView();
        var track = state.AddTrack();
        state.SetTrackColor(track, 3);
        state.PlaceTrack(track, 0, 0);
        view.Layout();

        var resting = view.FillOf(view.Blocks[0]);
        Assert.Equal(view.ClipPalette[3], resting);

        Press(ctx, view, 10, 30); // the clip starts at x 0 on lane 0
        Release(ctx, view, 10, 30);
        view.Layout();

        var selected = view.FillOf(view.Blocks[0]);
        Assert.True(state.SelectedPlacements.Count == 1, "the press did not select the clip");

        // Brighter on every channel.
        Assert.True(selected.X > resting.X && selected.Y > resting.Y && selected.Z > resting.Z,
            $"{selected} is not brighter than {resting}");
        Assert.Equal(resting.W, selected.W);

        // Still recognizably the same color: the channels keep their order against each
        // other, which a fixed selection shade would flatten away.
        Assert.Equal(resting.X.CompareTo(resting.Y), selected.X.CompareTo(selected.Y));
        Assert.Equal(resting.Y.CompareTo(resting.Z), selected.Y.CompareTo(selected.Z));

        // A differently colored track lifts to a different shade - the whole point of
        // deriving it instead of painting one fixed selection color.
        var other = state.AddTrack();
        state.SetTrackColor(other, 0);
        state.PlaceTrack(other, 1, 0);
        view.Layout();
        Assert.NotEqual(selected, view.ColorOf(other, true));
    }

    [Fact]
    public void EveryClipFill_CarriesItsLabel_AtRestAndSelected()
    {
        var (_, _, view) = NewView();

        // 3:1 is WCAG AA for large text and UI components - the bar a 13px semibold name
        // over a colored block has to clear. The guard on the sheet's one label color: if
        // a palette entry is ever retuned dark enough to fail here, the arrangement needs
        // the light shade back (per fill, or for the lot), not a quietly unreadable clip.
        foreach (var fill in view.ClipPalette.Append(view.ClipColor))
        foreach (var shade in new[] { fill, Lift(fill) })
            Assert.True(Contrast(view.ClipLabelColor, shade) >= 3f,
                $"{shade} carries its label at only {Contrast(view.ClipLabelColor, shade):0.00}:1");
        return;

        // Mirrors ArrangementView's own selected lift - deliberately a second
        // implementation, so a change to either one has to be meant.
        static Vector4 Lift(Vector4 color)
        {
            return new Vector4(Vector3.Lerp(color.Xyz, Vector3.One, 0.35f), color.W);
        }

        static float Contrast(Vector4 text, Vector4 background)
        {
            var (a, b) = (Luminance(text), Luminance(background));
            return (Math.Max(a, b) + 0.05f) / (Math.Min(a, b) + 0.05f);
        }

        static float Luminance(Vector4 color)
        {
            static float Linear(float c)
            {
                return c <= 0.03928f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
            }

            return 0.2126f * Linear(color.X) + 0.7152f * Linear(color.Y) + 0.0722f * Linear(color.Z);
        }
    }
}

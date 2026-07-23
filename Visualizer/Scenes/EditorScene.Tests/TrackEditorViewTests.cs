using EditorScene.Scenes.Components;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

// Drives UIContext.UpdatePointer directly with primitives, like ArrangementViewTests.
// Geometry: view is 800x414, so the grid is 392 px tall and every value row is
// exactly 8 px; the default segment is 16 steps of 16 px starting at the 44 px gutter.
// The beat ruler adds RulerHeight (18px) above the old grid top; since the initial
// layout also centers value 0 vertically, every row-targeting y below shifts by
// exactly half that (+9), not the full 18 — the click coordinates already bake it in.
public class TrackEditorViewTests
{
    private static Instrument MakeInstrument(EditorState state, string sound)
    {
        var instrument = state.AddInstrument(sound);
        state.SetInstrumentSounds(instrument, [sound]);
        return instrument;
    }

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

    private static (EditorTestContext ctx, EditorState state, TrackEditorView view, ProjectTrack track) NewView()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        state.OpenTrack(track);
        // The default zoom is 64 px/step and rows default to a fixed 20 px minimum;
        // these tests keep the original 16 px / stretch-to-fit (8 px rows) geometry.
        var view = new TrackEditorView(ctx, state)
            { Width = 800, Height = 414, PixelsPerStep = 16f, RowHeight = 8f };
        state.OnProjectChanged += view.InvalidateLayout; // the EditorInterface wiring
        view.Layout();
        return (ctx, state, view, track);
    }

    [Fact]
    public void ClickOnAnEmptyCell_PlacesTheActiveInstrument_AtIntegerStepAndValue()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        state.ActiveInstrument = boom;

        // Step 3 (x = 44 + 3*16 + 8), value 0 (row center y = 22 + 24*8 + 4 + 9).
        Click(ctx, view, 100, 227);

        var note = Assert.Single(track.Segments[0].Notes);
        Assert.Equal(3, note.Step);
        Assert.Equal(0, note.Value);
        Assert.Same(boom, note.Instrument);
        Assert.Same(note, state.SelectedNote);
        Assert.Same(track.Segments[0], state.SelectedSegment);
    }

    [Fact]
    public void PressAndSweep_PaintsANoteIntoEveryCellCrossed()
    {
        var (ctx, state, view, track) = NewView();
        state.ActiveInstrument = MakeInstrument(state, "boom");

        // The first note exists as soon as the button goes down (FL paint style)…
        Press(ctx, view, 100, 227); // step 3, value 0
        Assert.Single(track.Segments[0].Notes);

        // …and sweeping while held paints every new cell crossed.
        Drag(ctx, view, 116, 227); // step 4, same row
        Drag(ctx, view, 132, 219); // step 5, value 1
        Drag(ctx, view, 132, 219); // resting in place must not stack duplicates
        Release(ctx, view, 132, 219);

        Assert.Equal([(3, 0d), (4, 0d), (5, 1d)],
            track.Segments[0].Notes.Select(n => (n.Step, n.Value)).ToList());
    }

    [Fact]
    public void HoldingRightAndSweeping_ErasesEveryNoteCrossed()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        foreach (var step in (int[])[3, 4, 5])
            state.AddNote(track.Segments[0], step, boom, 0);
        view.Layout();

        // The right button stays held while the pointer crosses all three notes;
        // the app lays out (and resettles the pool) between frames.
        foreach (var x in (float[])[100, 116, 132])
        {
            ctx.UpdatePointer(view, x, 227, false, false, false, Vector2.Zero, true);
            view.Layout();
        }

        Assert.Empty(track.Segments[0].Notes);
    }

    [Fact]
    public void ClickWithoutAnActiveInstrument_JustDeselects()
    {
        var (ctx, state, view, track) = NewView();
        state.ActiveInstrument = null;
        var existing = state.AddNote(track.Segments[0], 0, MakeInstrument(state, "boom"), 0);
        state.SelectNote(existing);
        view.Layout();

        Click(ctx, view, 500, 218);

        Assert.Null(state.SelectedNote);
        Assert.Equal([existing], track.Segments[0].Notes); // nothing new was placed
    }

    [Fact]
    public void ClicksInTheGutter_NeverPlaceNotes()
    {
        var (ctx, state, view, track) = NewView();
        state.ActiveInstrument = MakeInstrument(state, "boom");

        Click(ctx, view, 20, 218); // gutter column

        Assert.Empty(track.Segments[0].Notes);
    }

    [Fact]
    public void DraggingANote_MovesItAcrossStepsValuesAndSegments()
    {
        var (ctx, state, view, track) = NewView();
        var second = state.AddSegment(track);
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 0);
        view.Layout();

        // Press the note (step 3, value 0), then one step right and one value up.
        Press(ctx, view, 100, 227);
        Assert.Same(note, state.SelectedNote);

        Drag(ctx, view, 116, 219);
        Assert.Equal(4, note.Step);
        Assert.Equal(1, note.Value);

        // Past the first segment's 256 px: lands on the second segment's step 0.
        Drag(ctx, view, 308, 219);
        Assert.Empty(track.Segments[0].Notes);
        Assert.Equal([note], second.Notes);
        Assert.Equal(0, note.Step);

        Release(ctx, view, 308, 219);
        view.Update(ctx); // drag-end housekeeping
        view.Layout(); // the app lays out every frame; the pool resettles here

        // The note is still selectable after the pool resettles.
        Press(ctx, view, 44 + 256 + 8, 219);
        Assert.Same(note, state.SelectedNote);
    }

    [Fact]
    public void FineSnap_PlacesFractionalValues_InFifthsOfASemitone()
    {
        var (ctx, state, view, track) = NewView();
        state.ActiveInstrument = MakeInstrument(state, "boom");
        view.FineSnap = true;

        // r = 24.1 rows -> value centered at 0.4.
        Click(ctx, view, 100, 22 + 24.1f * 8 + 9);

        var note = Assert.Single(track.Segments[0].Notes);
        Assert.Equal(0.4, note.Value, 3);
    }

    [Fact]
    public void PlacingAndDragging_FireThePreviewSeam_OnlyOnCellChanges()
    {
        var (ctx, state, view, _) = NewView();
        var boom = MakeInstrument(state, "boom");
        state.ActiveInstrument = boom;
        var previews = new List<(Instrument instrument, double value)>();
        view.OnPreviewNote = (i, v) => previews.Add((i, v));

        // Placing previews the new note.
        Click(ctx, view, 100, 227); // step 3, value 0
        Assert.Equal([(boom, 0d)], previews);
        view.Layout(); // the app lays out every frame; the new note gets its block

        // Pressing the note does not re-preview; dragging within the same cell
        // neither; crossing to a new value previews once, at the new pitch.
        Press(ctx, view, 100, 227);
        Drag(ctx, view, 102, 228);
        Assert.Single(previews);

        Drag(ctx, view, 100, 219); // one value up
        Assert.Equal((boom, 1d), previews[^1]);
        Assert.Equal(2, previews.Count);
        Release(ctx, view, 100, 219);
    }

    [Fact]
    public void RightClickOnANote_RemovesIt()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 0);
        state.SelectNote(note);
        view.Layout();

        // Right press on the note (step 3, value 0). No left press involved.
        ctx.UpdatePointer(view, 100, 227, false, false, false, Vector2.Zero, true);

        Assert.Empty(track.Segments[0].Notes);
        Assert.Null(state.SelectedNote);

        // Right-clicking empty grid does nothing (no placement, no crash).
        ctx.UpdatePointer(view, 100, 227, false, false, false, Vector2.Zero, true);
        Assert.Empty(track.Segments[0].Notes);
    }

    [Fact]
    public void DeleteKey_RemovesTheSelectedNote()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 0);
        view.Layout();

        Click(ctx, view, 100, 227); // selects the note and focuses the view
        Assert.Same(note, state.SelectedNote);

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Delete, 0, 0, false));

        Assert.Empty(track.Segments[0].Notes);
        Assert.Null(state.SelectedNote);
    }

    [Fact]
    public void EscapeKey_ClosesTheTrack()
    {
        var (ctx, state, view, _) = NewView();

        Click(ctx, view, 500, 300); // focuses the view
        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Escape, 0, 0, false));

        Assert.Null(state.OpenedTrack);
    }

    [Fact]
    public void PlacingIntoADenseChart_StillRendersTheNewNote()
    {
        // BMS-dense Aleph-0 case: enough visible notes to stress the block pool. A
        // freshly placed note is appended to its segment's list, so pool exhaustion
        // drops exactly the newest note — it lands in the model but never renders,
        // and the next click on its cell places a duplicate instead of hitting it.
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        state.AddSegment(track);
        state.AddSegment(track); // 48 steps: the 800 px view shows ~47
        foreach (var segment in track.Segments)
            for (var step = 0; step < segment.StepCount; step++)
                for (var i = 0; i < 6; i++)
                    state.AddNote(segment, step, boom, 2 + i);
        state.ActiveInstrument = boom;
        view.Layout();
        var before = track.Segments.Sum(s => s.Notes.Count);

        // An empty zero-row cell in the LAST segment (step 40) — appended there, the
        // new note is the very last one the pool scan reaches.
        Click(ctx, view, 692, 227);
        view.Layout(); // the app lays out every frame

        // The second click must land on the new note's block (which swallows it),
        // not on empty grid — that would place a duplicate.
        Click(ctx, view, 692, 227);

        Assert.Equal(before + 1, track.Segments.Sum(s => s.Notes.Count));
        Assert.Equal(0, state.SelectedNote!.Value);
    }

    [Fact]
    public void CtrlWheel_ZoomsTheGrid_AnchoredAtThePointer()
    {
        var (ctx, state, view, track) = NewView();
        state.AddSegment(track);
        state.AddSegment(track);
        state.AddSegment(track); // 64 steps = 1024 px of content, so zoom can scroll
        state.ActiveInstrument = MakeInstrument(state, "boom");
        view.Layout();
        view.WheelZooms = true;

        // Wheel up over step 20 (x = 44 + 20*16 + a few px into the step).
        ctx.UpdatePointer(view, 372, 218, false, false, false, new Vector2(0, 1));
        Assert.True(view.PixelsPerStep > 16f);

        // The step under the cursor stayed put: clicking there places at step 20,
        // which is the second segment's local step 4.
        Click(ctx, view, 372, 218);
        var note = Assert.Single(track.Segments[1].Notes);
        Assert.Equal(4, note.Step);
    }

    [Fact]
    public void ClickOnTheZeroRow_PlacesThroughTheFullAppTree()
    {
        // Reproduces the app's exact nesting (root -> grid area -> vertical flex of
        // toolbar + editor) instead of using the view as the pointer root — an
        // occluder anywhere in that tree would break placement only in the app.
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        state.OpenTrack(track);
        state.ActiveInstrument = MakeInstrument(state, "boom");

        var root = new Sundex.Components.Panels.Panel(ctx) { Width = 1200, Height = 800 };
        var gridArea = new Sundex.Components.Panels.FlexPanel(ctx) { X = 260, Y = 56, Width = 940, Height = 744 };
        root.AddChild(gridArea);

        var view = new TrackEditorView(ctx, state)
        {
            Width = Sundex.Components.Abstractions.Values.LiteralOrComputable.Percent(100),
            Height = Sundex.Components.Abstractions.Values.LiteralOrComputable.Percent(100),
            PixelsPerStep = 16f,
            RowHeight = 8f // stretch-to-fit: the rowHeight formula below stays exact
        };
        var bar = new Sundex.Components.Panels.FlexPanel(ctx)
        {
            Width = Sundex.Components.Abstractions.Values.LiteralOrComputable.Percent(100),
            Height = 40,
            Spacing = 12,
            Padding = 6,
            Children =
            [
                new Sundex.Components.Labels.Button(ctx, "← Arrangement"),
                new Sundex.Components.Labels.Label(ctx, "Track 1"),
                new Sundex.Components.Labels.Button(ctx, "Instrument: —")
            ]
        };
        var editorPanel = new Sundex.Components.Panels.FlexPanel(ctx)
        {
            Direction = Sundex.Components.Abstractions.LayoutDirection.Vertical,
            Width = Sundex.Components.Abstractions.Values.LiteralOrComputable.Percent(100),
            Height = Sundex.Components.Abstractions.Values.LiteralOrComputable.Percent(100),
            Children = [bar, view]
        };
        gridArea.AddChild(editorPanel);
        root.Layout();

        // Click the exact center of the rendered zero band, wherever layout put it.
        var rowHeight = (view.Computed.Height - TrackEditorView.GridTop) / TrackEditorView.Rows;
        var x = view.Computed.AbsoluteX + TrackEditorView.GutterWidth + 100;
        var y = view.Computed.AbsoluteY + TrackEditorView.GridTop +
                TrackEditorView.MaxValue * rowHeight + rowHeight / 2;
        Click(ctx, root, x, y);

        var note = Assert.Single(track.Segments[0].Notes);
        Assert.Equal(0, note.Value);
    }

    [Fact]
    public void FixedRowHeight_ScrollsVertically_StartingCenteredOnZero()
    {
        // Default 20 px rows: 49 rows = 980 px of content in a 392 px viewport, so the
        // view scrolls instead of compressing. The first layout centers on value 0:
        // scrollY = (980 - 392) / 2 = 294.
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        state.OpenTrack(track);
        state.ActiveInstrument = MakeInstrument(state, "boom");
        var view = new TrackEditorView(ctx, state) { Width = 800, Height = 414, PixelsPerStep = 16f };
        state.OnProjectChanged += view.InvalidateLayout;
        view.Layout();

        // Mid-viewport row: with GridTop=40 the centered scrollY is 1023 (not 294 —
        // that assumed the old 22px StripHeight and a stale Rows count); the ruler
        // only shifts the click y by +9 (RulerHeight/2) vs. the pre-ruler geometry.
        // r = (205 - 40 + 1023) / 20 = 59.4 → value 60 - 59 = 1.
        Click(ctx, view, 100, 205);
        Assert.Equal(1, Assert.Single(track.Segments[0].Notes).Value);

        // Wheel down scrolls the rows: scrollY 1023 → 1071, the same +9-shifted y
        // now maps to r = (205 - 40 + 1071) / 20 = 61.8 → value -1.
        ctx.UpdatePointer(view, 400, 200, false, false, false, new Vector2(0, -1));
        Click(ctx, view, 116, 205);
        Assert.Contains(track.Segments[0].Notes, n => n.Value == -1);
    }

    [Fact]
    public void MiddleDrag_PansBothAxes_OnlyWhenStartedInsideTheView()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        state.OpenTrack(track);
        state.AddSegment(track);
        state.AddSegment(track); // 48 steps = 768 px, so there is room to pan into
        state.ActiveInstrument = MakeInstrument(state, "boom");
        var view = new TrackEditorView(ctx, state) { Width = 400, Height = 414, PixelsPerStep = 16f };
        state.OnProjectChanged += view.InvalidateLayout;
        view.Layout(); // centers: scrollY = (2420 - 374) / 2 = 1023 (GridTop = 40)

        // Hold middle inside the view and drag up-left by (48, 40):
        // scrollX 0 → 48 (3 steps), scrollY 1023 → 1063 (2 rows).
        view.MiddlePan(true, 200, 200);
        view.MiddlePan(true, 152, 160);
        view.MiddlePan(false, 152, 160);
        view.Layout();

        // Step at x=100: (100 - 44 + 48) / 16 = 6.5 → step 6.
        // Value at the +9-shifted y=205: r = (205 - 40 + 1063) / 20 = 61.4 → value -1.
        Click(ctx, view, 100, 205);
        var note = Assert.Single(track.Segments[0].Notes);
        Assert.Equal(6, note.Step);
        Assert.Equal(-1, note.Value);

        // A hold that starts outside the view must never pan it: the same click maps
        // to the same cell, where painting an identical note is suppressed.
        view.MiddlePan(true, 1000, 1000);
        view.MiddlePan(true, 900, 900);
        view.MiddlePan(false, 900, 900);
        view.Layout();
        Click(ctx, view, 100, 205);
        Assert.Single(track.Segments[0].Notes);
    }

    [Fact]
    public void AutomationDrawsAStepPath_RunJumpAndTickPerGeneratedEvent()
    {
        var (_, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 0);
        note.Automation = new AudioKeyframeManager
        {
            Repeats = 2,
            Keyframes = { new AudioKeyframe { Gap = 2, Value = new Modifier(12) } }
        };
        view.Layout();

        // Two generated events (steps 5 and 7, values 12 and 24), each drawn as a
        // horizontal run + a vertical jump + a tick = 6 visible marks.
        var visible = view.AutomationMarks.Where(m => m.Width.Value > 0).ToList();
        Assert.Equal(6, visible.Count);

        // The path starts at the note's center (x = 44 + 3.5*16 = 100) and runs the
        // 2-step gap (32 px) to the first event at x = 132.
        Assert.Equal(100, visible[0].X.Value);
        Assert.Equal(32, visible[0].Width.Value);
        // The jump covers the 12-row value change (96 px at 8 px rows)…
        Assert.Equal(96, visible[1].Height.Value);
        // …and the tick straddles the event column.
        Assert.Equal(131, visible[2].X.Value);

        // Removing the automation hides the whole path on the next layout.
        note.Automation = null;
        view.InvalidateLayout();
        view.Layout();
        Assert.DoesNotContain(view.AutomationMarks, m => m.Width.Value > 0);
    }

    [Fact]
    public void ClickingTheStrip_SelectsTheSegmentUnderThePointer()
    {
        var (ctx, state, view, track) = NewView();
        var second = state.AddSegment(track);
        view.Layout();

        // The second segment's strip block starts at 44 + 256 px.
        Click(ctx, view, 310, 10);

        Assert.Same(second, state.SelectedSegment);
    }

    [Fact]
    public void BeatLabels_ShowRunningBeatNumberAcrossSegments()
    {
        var (_, state, view, track) = NewView();
        state.AddSegment(track); // default 16 steps / 4 beats, same as segment 1
        view.Layout();

        // Segment 1 contributes 4 beats, so segment 2's first beat is the 5th overall,
        // at the pixel where segment 2 starts (44 + 16*16 = 300).
        var label = view.BeatLabels.Single(l => l.X.Value > -1000f && l.Value.ToString() == "5");
        Assert.Equal(302, label.X.Value);
    }

    [Fact]
    public void BeatLabels_AlignToBeatBoundarySteps_NotEveryStep()
    {
        var (_, _, view, _) = NewView();
        view.Layout();

        // One segment, 4 beats of 4 steps each at 16 px/step: labels every 64 px,
        // not every 16 px step — proves beat-stride, not step-stride.
        var visible = view.BeatLabels.Where(l => l.X.Value > -1000f)
            .OrderBy(l => l.X.Value).ToList();
        Assert.Equal(["1", "2", "3", "4"], visible.Select(l => l.Value.ToString()).ToList());
        Assert.Equal([46f, 110f, 174f, 238f], visible.Select(l => l.X.Value).ToList());
    }

    [Fact]
    public void BeatLabels_HighlightTheBeatContainingThePlayhead()
    {
        var (_, state, view, track) = NewView();
        state.PlaceTrack(track, 0, 0);
        // 120 BPM, 4 steps/beat: beat 2 starts at step 4 = 1 quarter note in.
        view.PlayheadQuarters = 1.0;
        view.Layout();

        var labels = view.BeatLabels.Where(l => l.X.Value > -1000f)
            .ToDictionary(l => l.Value.ToString());
        Assert.NotEqual(labels["1"].Color, labels["2"].Color);
    }

    [Fact]
    public void BeatLabels_ThinOutAtLowZoom_ToAvoidOverlap()
    {
        var (_, state, view, track) = NewView();
        for (var i = 0; i < 20; i++) state.AddSegment(track); // enough beats to fill the view
        view.PixelsPerStep = 4f; // min zoom: 4 px/step * 4 steps/beat = 16 px/beat, under MinBeatLabelSpacingPx
        view.Layout();

        var xs = view.BeatLabels.Where(l => l.X.Value > -1000f)
            .Select(l => l.X.Value).OrderBy(x => x).ToList();
        Assert.True(xs.Count > 1);
        for (var i = 1; i < xs.Count; i++)
            Assert.True(xs[i] - xs[i - 1] >= 28f, $"labels at {xs[i - 1]} and {xs[i]} overlap");
    }
}

using EditorScene.State;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Views;

namespace EditorScene.Tests;

// Drives UIContext.UpdatePointer directly with primitives, like ArrangementViewTests.
// Geometry: view is 800x414, GridTop = StripHeight + RulerHeight = 40; the cut row is
// pinned to the bottom instead, so it never affects GridTop or these row-targeting y
// values. With RowHeight set to 8px, the
// initial CenterPending centers scrollY at (Rows*8 - gridHeight)/2 = 309.5. Value v's
// row top is ValueTop(v) = GridTop + (60-v)*8 - scrollY; the row-targeting y values
// below sit mid-row (+4) inside that. The default segment is 16 steps of 16 px
// starting at the 44 px gutter.
public class TrackEditorViewTests
{
    private static Instrument MakeInstrument(EditorState state, string sound)
    {
        var instrument = state.AddInstrument(sound);
        state.SetInstrumentSounds(instrument, [new InstrumentSound { Sound = sound }]);
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
        // The default zoom is 64 px/step and rows default to 20 px; these tests keep
        // the original 16 px / 8 px-row geometry.
        // Styled like the editor styles it: the grid's colors are settings on the view
        // (class note-canvas), so an unstyled one paints everything transparent.
        var view = EditorTestContext.Styled(new TrackEditorView(ctx, state)
            { Width = 800, Height = 414, PixelsPerStep = 16f, RowHeight = 8f });
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

        // Step 3 (x = 44 + 3*16 + 8), value 0 (row center y = 22 + 24*8 + 4 + 9 + 8 cut-band shift).
        Click(ctx, view, 100, 214.5f);

        var note = Assert.Single(track.Segments[0].Notes);
        Assert.Equal(3, note.Step);
        Assert.Equal(0, note.Value);
        Assert.Same(boom, note.Instrument);
        Assert.Same(note, state.SelectedNote);
        Assert.Same(track.Segments[0], state.SelectedSegment);
    }

    [Fact]
    public void ClickingANote_MakesItsInstrumentTheOneThatPaints()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var hat = MakeInstrument(state, "hat");

        state.ActiveInstrument = boom;
        Click(ctx, view, 100, 214.5f); // step 3, value 0 - a boom
        state.ActiveInstrument = hat;
        Click(ctx, view, 132, 214.5f); // step 5 - a hat
        view.Layout(); // the blocks the clicks below land on

        var notified = 0;
        state.OnInstrumentsChanged += () => notified++;

        // Back onto the boom: picking it is what lets the next click paint one.
        Click(ctx, view, 100, 214.5f);

        Assert.Same(boom, state.ActiveInstrument);
        Assert.Equal(1, notified); // the tool bar's button reads the event, not the field

        Click(ctx, view, 132, 214.5f);
        Assert.Same(hat, state.ActiveInstrument);
        Assert.Equal(2, track.Segments[0].Notes.Count); // clicks landed on notes, not on cells
    }

    [Fact]
    public void PickingTheInstrumentAlreadyActive_NotifiesNobody()
    {
        var (ctx, state, view, track) = NewView();
        state.ActiveInstrument = MakeInstrument(state, "boom");
        Click(ctx, view, 100, 214.5f);
        view.Layout();

        var notified = 0;
        state.OnInstrumentsChanged += () => notified++;
        Click(ctx, view, 100, 214.5f);

        Assert.Equal(0, notified);
        Assert.Single(track.Segments[0].Notes);
    }

    [Fact]
    public void PressAndSweep_PaintsANoteIntoEveryCellCrossed()
    {
        var (ctx, state, view, track) = NewView();
        state.ActiveInstrument = MakeInstrument(state, "boom");

        // The first note exists as soon as the button goes down (FL paint style)…
        Press(ctx, view, 100, 214.5f); // step 3, value 0
        Assert.Single(track.Segments[0].Notes);

        // …and sweeping while held paints every new cell crossed.
        Drag(ctx, view, 116, 214.5f); // step 4, same row
        Drag(ctx, view, 132, 206.5f); // step 5, value 1
        Drag(ctx, view, 132, 206.5f); // resting in place must not stack duplicates
        Release(ctx, view, 132, 206.5f);

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
            ctx.UpdatePointer(view, x, 214.5f, false, false, false, Vector2.Zero, true);
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
        Press(ctx, view, 100, 214.5f);
        Assert.Same(note, state.SelectedNote);

        Drag(ctx, view, 116, 206.5f);
        Assert.Equal(4, note.Step);
        Assert.Equal(1, note.Value);

        // Past the first segment's 256 px: lands on the second segment's step 0.
        Drag(ctx, view, 308, 206.5f);
        Assert.Empty(track.Segments[0].Notes);
        Assert.Equal([note], second.Notes);
        Assert.Equal(0, note.Step);

        Release(ctx, view, 308, 206.5f);
        view.Update(ctx); // drag-end housekeeping
        view.Layout(); // the app lays out every frame; the pool resettles here

        // The note is still selectable after the pool resettles.
        Press(ctx, view, 44 + 256 + 8, 206.5f);
        Assert.Same(note, state.SelectedNote);
    }

    [Fact]
    public void ClickingANote_WithAFractionalValue_LeavesItUntouched()
    {
        // The real render loop calls UIContext.UpdatePointer every frame, so a plain held
        // click isn't just press-then-release: the button is still down on the frame after the
        // press, which fires a same-position "drag" too (the Click helper skips that frame).
        // Under the Select tool a click must not move or resnap the note at all.
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 6.4);
        view.Layout();
        state.ActiveTool = EditorTool.Select;

        Press(ctx, view, 100, 163.3f); // step 3, value 6.4
        Drag(ctx, view, 100, 163.3f); // same position: the input dispatcher's held-click frame
        Release(ctx, view, 100, 163.3f);

        Assert.Equal(6.4, note.Value, 3);
        Assert.Equal(3, note.Step);
    }

    [Fact]
    public void DraggingANote_WithAFractionalValue_PreservesTheFraction()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 6.4);
        view.Layout();

        Press(ctx, view, 100, 163.3f); // step 3, value 6.4
        Drag(ctx, view, 100, 155.3f); // one row up
        Release(ctx, view, 100, 155.3f);

        Assert.Equal(7.4, note.Value, 3); // not 7 - the fraction survives the drag
        Assert.Equal(3, note.Step);
    }

    [Fact]
    public void FineSnap_PlacesFractionalValues_InFifthsOfASemitone()
    {
        var (ctx, state, view, track) = NewView();
        state.ActiveInstrument = MakeInstrument(state, "boom");
        view.FineSnap = true;

        // r = 60.1 rows -> value centered at 0.4.
        Click(ctx, view, 100, 211.3f);

        var note = Assert.Single(track.Segments[0].Notes);
        Assert.Equal(0.4, note.Value, 3);
    }

    [Fact]
    public void PlacingAndDragging_FireThePreviewSeam_OnlyOnCellChanges()
    {
        var (ctx, state, view, _) = NewView();
        var boom = MakeInstrument(state, "boom");
        state.ActiveInstrument = boom;
        // The seam hands over the note itself now, so a preview carries its volume and pan;
        // a drag passes the value it is heading for, before the note lands there.
        var previews = new List<(Instrument instrument, double value)>();
        view.OnPreviewNote = (note, value) => previews.Add((note.Instrument, value ?? note.Value));

        // Placing previews the new note.
        Click(ctx, view, 100, 214.5f); // step 3, value 0
        Assert.Equal([(boom, 0d)], previews);
        view.Layout(); // the app lays out every frame; the new note gets its block

        // Pressing the note does not re-preview; dragging within the same cell
        // neither; crossing to a new value previews once, at the new pitch.
        Press(ctx, view, 100, 214.5f);
        Drag(ctx, view, 102, 215.5f);
        Assert.Single(previews);

        Drag(ctx, view, 100, 206.5f); // one value up
        Assert.Equal((boom, 1d), previews[^1]);
        Assert.Equal(2, previews.Count);
        Release(ctx, view, 100, 206.5f);
    }

    [Fact]
    public void RightClickOnANote_RemovesIt()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 0);
        state.SelectNote(note);
        view.Layout();

        // Right press on the note (step 3, value 0). No left press involved.
        ctx.UpdatePointer(view, 100, 214.5f, false, false, false, Vector2.Zero, true);

        Assert.Empty(track.Segments[0].Notes);
        Assert.Null(state.SelectedNote);

        // Right-clicking empty grid does nothing (no placement, no crash).
        ctx.UpdatePointer(view, 100, 214.5f, false, false, false, Vector2.Zero, true);
        Assert.Empty(track.Segments[0].Notes);
    }

    [Fact]
    public void DeleteKey_RemovesTheSelectedNote()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 0);
        view.Layout();

        Click(ctx, view, 100, 214.5f); // selects the note and focuses the view
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
        // drops exactly the newest note - it lands in the model but never renders,
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

        // An empty zero-row cell in the LAST segment (step 40) - appended there, the
        // new note is the very last one the pool scan reaches.
        Click(ctx, view, 692, 214.5f);
        view.Layout(); // the app lays out every frame

        // The second click must land on the new note's block (which swallows it),
        // not on empty grid - that would place a duplicate.
        Click(ctx, view, 692, 214.5f);

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
        // toolbar + editor) instead of using the view as the pointer root - an
        // occluder anywhere in that tree would break placement only in the app.
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        state.OpenTrack(track);
        state.ActiveInstrument = MakeInstrument(state, "boom");

        var root = new Panel(ctx) { Width = 1200, Height = 800 };
        var gridArea = new FlexPanel(ctx) { X = 260, Y = 56, Width = 940, Height = 744 };
        root.AddChild(gridArea);

        var view = new TrackEditorView(ctx, state)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            PixelsPerStep = 16f,
            RowHeight = 8f
        };
        var bar = new FlexPanel(ctx)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 40,
            Spacing = 12,
            Padding = 6,
            Children =
            [
                new Button(ctx, "← Arrangement"),
                new Label(ctx, "Track 1"),
                new Button(ctx, "Instrument: -")
            ]
        };
        var editorPanel = new FlexPanel(ctx)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Children = [bar, view]
        };
        gridArea.AddChild(editorPanel);
        root.Layout();

        // Click the exact center of the rendered zero band, wherever layout put it.
        var gridBottom = view.Computed.Height - TrackEditorView.CutRowHeight - TrackEditorGeometry.RuleHeight;
        var rowHeight = (gridBottom - TrackEditorView.GridTop) / TrackEditorView.Rows;
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
        // Default 20 px rows: the pinned cut row moves GridBottom to height - 25, so
        // gridHeight = 414 - 40 - 25 = 349 and 121 rows at 20px (2420) overflow it, so
        // the view scrolls instead of compressing. The first layout centers on value 0:
        // scrollY = (2420 - 349) / 2 = 1035.5.
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        state.OpenTrack(track);
        state.ActiveInstrument = MakeInstrument(state, "boom");
        var view = new TrackEditorView(ctx, state) { Width = 800, Height = 414, PixelsPerStep = 16f };
        state.OnProjectChanged += view.InvalidateLayout;
        view.Layout();

        // Mid-viewport row: r = (194.5 - 40 + 1035.5) / 20 = 59.5 → value 60-59 = 1.
        Click(ctx, view, 100, 194.5f);
        Assert.Equal(1, Assert.Single(track.Segments[0].Notes).Value);

        // Wheel down scrolls the rows: scrollY 1035.5 → 1083.5, the same y now maps to
        // r = (194.5 - 40 + 1083.5) / 20 = 61.9 → value 60-61 = -1.
        ctx.UpdatePointer(view, 400, 200, false, false, false, new Vector2(0, -1));
        Click(ctx, view, 116, 194.5f);
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
        view.Layout(); // centers: scrollY = (2420 - 349) / 2 = 1035.5 (GridTop = 40, gridHeight = 349)

        // Hold middle inside the view and drag up-left by (48, 40):
        // scrollX 0 → 48 (3 steps), scrollY 1035.5 → 1075.5 (2 rows).
        view.MiddlePan(true, 200, 200);
        view.MiddlePan(true, 152, 160);
        view.MiddlePan(false, 152, 160);
        view.Layout();

        // Step at x=100: (100 - 44 + 48) / 16 = 6.5 → step 6.
        // Value at y=194.5: r = (194.5 - 40 + 1075.5) / 20 = 61.5 → value 60-61 = -1.
        Click(ctx, view, 100, 194.5f);
        var note = Assert.Single(track.Segments[0].Notes);
        Assert.Equal(6, note.Step);
        Assert.Equal(-1, note.Value);

        // A hold that starts outside the view must never pan it: the same click maps
        // to the same cell, where painting an identical note is suppressed.
        view.MiddlePan(true, 1000, 1000);
        view.MiddlePan(true, 900, 900);
        view.MiddlePan(false, 900, 900);
        view.Layout();
        Click(ctx, view, 100, 194.5f);
        Assert.Single(track.Segments[0].Notes);
    }

    [Fact]
    public void CtrlMiddleDrag_ScalesTheRowHeight_InsteadOfPanning()
    {
        var (_, _, view, _) = NewView();
        view.WheelZooms = true; // Ctrl held

        // Drag up by 50 px: rows grow, and the pan is suppressed.
        view.MiddlePan(true, 200, 200);
        view.MiddlePan(true, 200, 150);
        view.MiddlePan(false, 200, 150);
        Assert.True(view.RowHeight > 8f);

        // A hold starting outside the view scales nothing.
        var height = view.RowHeight;
        view.MiddlePan(true, 1000, 1000);
        view.MiddlePan(true, 1000, 900);
        view.MiddlePan(false, 1000, 900);
        Assert.Equal(height, view.RowHeight);

        // Dragging far past either end pins at the 4 px / 300 px limits.
        view.MiddlePan(true, 200, 200);
        view.MiddlePan(true, 200, -2000);
        Assert.Equal(TrackEditorGeometry.MaxRowHeight, view.RowHeight);
        view.MiddlePan(true, 200, 2000);
        view.MiddlePan(false, 200, 2000);
        Assert.Equal(TrackEditorGeometry.MinRowHeight, view.RowHeight);
    }

    [Fact]
    public void AutomationDrawsALeaningPath_LineAndTickPerGeneratedEvent_AndAnEndCap()
    {
        var (_, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 0);
        note.Automation = new AudioKeyframeManager
        {
            Gap = 2,
            Template = new AudioKeyframe { Value = new Modifier(12) },
            End = 6
        };
        view.Layout();

        // Two generated events (2 and 4 steps in, values 12 and 24), each drawn as a
        // connecting line + a tick, and the cut-at-end cap = 5 visible marks.
        var visible = view.AutomationMarks.Where(m => m.Z > 0).ToList();
        Assert.Equal(5, visible.Count);

        // The path starts at the note's center (x = 44 + 3.5*16 = 100) and leans to the
        // first event, 2 steps (32 px) right and 12 rows (96 px) up: the line is longer
        // than either leg, which a stepped run-then-jump path could never be.
        Assert.Equal(100, visible[0].X);
        Assert.Equal(MathF.Sqrt(32 * 32 + 96 * 96), visible[0].Z, 3);
        // The tick straddles the event column.
        Assert.Equal(131, visible[1].X);

        // The cap sits on the note's right edge: 6 steps past x = 44 + 3*16 = 92.
        Assert.Equal(92 + 6 * 16 - 2, visible[4].X);
        Assert.Equal(2, visible[4].Z);

        // Nothing marks the end when the note is left to ring on.
        note.Automation.CutAtEnd = false;
        view.InvalidateLayout();
        view.Layout();
        Assert.Equal(4, view.AutomationMarks.Count(m => m.Z > 0));

        // Removing the automation hides the whole path on the next layout.
        note.Automation = null;
        view.InvalidateLayout();
        view.Layout();
        Assert.DoesNotContain(view.AutomationMarks, m => m.Z > 0);
    }

    [Fact]
    public void ARepairedNote_GetsAHalfTickWhereAnotherNoteCutIt_AndNoHandle()
    {
        var (_, state, view, track) = NewView();
        var pad = MakeInstrument(state, "pad");

        // Two held notes on one instrument, no retriggers of their own - so the only cut on
        // the timeline is the short one's end cut, four steps in, and it lands inside the
        // long one. Auto-resume puts the long note back there.
        var held = state.AddNote(track.Segments[0], 0, pad, 0);
        held.Automation = new AudioKeyframeManager { Gap = 0, End = 6 };
        var shorter = state.AddNote(track.Segments[0], 2, pad, 0);
        shorter.Automation = new AudioKeyframeManager { Gap = 0, End = 2 };

        view.InvalidateLayout();
        view.Layout();

        // The held note's resume at step 4, then its own end cap at step 6, then the short
        // note's cap - also at step 4, where it cut. Only the resume is one pixel wide.
        var visible = view.AutomationMarks.Where(m => m.Z > 0).ToList();
        Assert.Equal([115.5f, 138f, 106f], visible.Select(m => m.X));
        Assert.Equal([1f, 2f, 2f], visible.Select(m => m.Z));

        // Nothing to drag at a resume: it is automatic, and has no keyframe behind it.
        Assert.Empty(view.KeyframeHandles);

        // Turned off, the note is simply cut and there is nothing to mark.
        state.Edit(() => state.Project.AutoResume = false);
        view.InvalidateLayout();
        view.Layout();
        Assert.Equal([138f, 106f], view.AutomationMarks.Where(m => m.Z > 0).Select(m => m.X));
    }

    [Fact]
    public void ALongNote_IsDrawnAndHitTestableAcrossItsWholeLength()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 2, MakeInstrument(state, "boom"), 0);
        // A gap as wide as the note: no markers sit on the body, which own presses over it.
        note.Automation = new AudioKeyframeManager { Gap = 3, End = 3 };
        view.Layout();

        // Three steps wide from x = 44 + 2*16 = 76, minus the 1 px gap between blocks.
        var block = view.NoteBlocks.Single(b => b.Note == note);
        Assert.Equal(76, block.Computed.X);
        Assert.Equal(3 * 16 - 1, block.Computed.Width);

        // A press near its far end still lands on the note, not on empty grid.
        state.SetNoteSelection([]);
        Press(ctx, view, 76 + 40, 214.5f);
        Assert.Same(note, Assert.Single(state.SelectedNotes));
        Release(ctx, view, 76 + 40, 214.5f);
    }

    [Fact]
    public void ALongNote_StartingLeftOfTheViewport_StillReachesIntoIt()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var segment = track.Segments[0];
        segment.Bars = 8; // 128 steps
        var note = state.AddNote(segment, 0, boom, 0);
        note.Automation = new AudioKeyframeManager { Gap = 4, End = 64 };
        view.Layout();

        // Pan 10 notches right (480 px = 30 steps): the note starts well off-screen left.
        ctx.UpdatePointer(view, 400, 300, false, false, false, new Vector2(-10, 0));
        view.Layout();

        var block = view.NoteBlocks.Single(b => b.Note == note);
        var blockX = (float)block.Computed.X;
        var blockWidth = (float)block.Computed.Width;
        Assert.True(blockX < 0, $"the body should start off-screen left, was at {blockX}");
        Assert.True(blockX + blockWidth > TrackEditorView.GutterWidth,
            "the body should still reach into the viewport");
        // And its marks are still drawn, culled to what is on screen.
        Assert.Contains(view.AutomationMarks, m => m.Z > 0 && m.X > TrackEditorView.GutterWidth);
    }

    [Fact]
    public void PressingALongNotePastItsStart_LeavesItWhereItIs()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 2, MakeInstrument(state, "boom"), 0);
        note.Automation = new AudioKeyframeManager { Gap = 4, End = 4 }; // 4 steps, no markers
        view.Layout();

        // Two steps into the body: the drag delta runs from the pointer, not from the
        // note's start, so grabbing it here must not snap its start to the cursor.
        Press(ctx, view, 44 + 4 * 16 + 4, 214.5f);
        Assert.Equal(2, note.Step);

        // And it still moves by what the pointer moves.
        Drag(ctx, view, 44 + 5 * 16 + 4, 214.5f);
        Assert.Equal(3, note.Step);
        Release(ctx, view, 44 + 5 * 16 + 4, 214.5f);
    }

    [Fact]
    public void DraggingTheRightBorder_SetsTheNotesLength_AsOneUndoEntry()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 2, MakeInstrument(state, "boom"), 0);
        view.Layout();

        // The block is at x = 76, 15 px wide: its right grab zone starts at 86.
        Press(ctx, view, 88, 214.5f);
        Drag(ctx, view, 44 + 5 * 16 + 8, 214.5f); // 3 steps right
        Release(ctx, view, 44 + 5 * 16 + 8, 214.5f);

        // A plain note had no automation at all - the drag gives it a cutting sustain that
        // holds for its whole length: the gap starts at zero, so nothing retriggers inside
        // it until one is typed in.
        Assert.Equal(4, note.Automation!.End);
        Assert.True(note.Automation.Cut);
        Assert.True(note.Automation.CutAtEnd);
        Assert.Equal(0, note.Automation.Gap);
        Assert.Empty(note.Automation.Keyframes);

        note.Automation.Gap = 1;
        Assert.Equal(3, note.Automation.Keyframes.Count); // one a step, inside the 4

        // The whole drag is one entry, and undoing it takes the automation back off.
        state.Undo();
        Assert.Null(note.Automation);
        Assert.Equal(2, note.Step);
    }

    [Fact]
    public void DraggingTheLeftBorder_MovesTheNote_AndKeepsItsEndWhereItWas()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 4, MakeInstrument(state, "boom"), 0);
        note.Automation = new AudioKeyframeManager { Gap = 1, End = 4 }; // steps 4..8
        view.Layout();

        Press(ctx, view, 44 + 4 * 16 + 1, 214.5f); // inside the left grab zone
        Drag(ctx, view, 44 + 2 * 16 + 8, 214.5f); // two steps left
        Assert.Equal(2, note.Step);
        Assert.Equal(6, note.Automation.End); // still ends at step 8

        // Dragging the left border past the end floors the note at one step.
        Drag(ctx, view, 44 + 12 * 16 + 8, 214.5f);
        Assert.Equal(7, note.Step);
        Assert.Equal(1, note.Automation.End);
        Release(ctx, view, 44 + 12 * 16 + 8, 214.5f);

        state.Undo();
        Assert.Equal(4, note.Step);
        Assert.Equal(4, note.Automation!.End);
    }

    [Fact]
    public void ResizingANoteOfTheSelection_ResizesTheWholeSelectionByOneDelta()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var segment = track.Segments[0];
        var a = state.AddNote(segment, 1, boom, 0);
        a.Automation = new AudioKeyframeManager { Gap = 1, End = 2 };
        var b = state.AddNote(segment, 6, boom, 1);
        b.Automation = new AudioKeyframeManager { Gap = 1, End = 4 };
        state.SetNoteSelection([a, b]);
        view.Layout();

        // a spans steps 1..3: press its right border and pull three steps out. The delta
        // is the pointer's, from where it pressed - grabbing a border mid-zone doesn't
        // snap the note to the pointer.
        Press(ctx, view, 44 + 3 * 16 - 2, 214.5f);
        Drag(ctx, view, 44 + 5 * 16 + 8, 214.5f);
        Release(ctx, view, 44 + 5 * 16 + 8, 214.5f);

        Assert.Equal(5, a.Automation!.End);
        Assert.Equal(7, b.Automation!.End); // same delta, its own start
    }

    [Fact]
    public void DraggingAKeyframeMarker_WritesItsValueAndItsOwnPosition()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 2, MakeInstrument(state, "boom"), 0);
        note.Automation = new AudioKeyframeManager { Gap = 1, End = 4 }; // markers 1, 2 and 3 steps in
        state.SelectNote(note); // markers are handles on the selected note only
        view.Layout();

        // The second marker sits at x = 44 + 2*16 + (0.5 + 2)*16 = 116, on value 0's row.
        var marker = view.KeyframeBlocks.Single(b => b.Note == note && b.KeyframeIndex == 1);
        Assert.Equal(116 - 5, marker.Computed.X);

        // Drag it half a step left and 3 rows up (8 px rows).
        Press(ctx, view, 116, 214.5f);
        Drag(ctx, view, 108, 214.5f - 24);

        var keyframe = note.Automation.Keyframes[1];
        Assert.Equal(1.5f, keyframe.Position!.Value, 2);
        Assert.Equal(new Modifier(3), keyframe.Value); // relative to the previous result
        // Its neighbours stay on the derived grid.
        Assert.Null(note.Automation.Keyframes[0].Position);
        Assert.Equal(3f, note.Automation.PositionOf(2));

        // Markers cannot cross: dragged past its neighbour it stops just short of it.
        Drag(ctx, view, 44 + 2 * 16 + 8, 214.5f - 24);
        Assert.InRange(keyframe.Position!.Value, 1f, 1.001f);
        Release(ctx, view, 44 + 2 * 16 + 8, 214.5f - 24);

        // And the whole drag is one undo entry.
        state.Undo();
        Assert.Null(note.Automation.Keyframes[1].Position);
        Assert.Equal(default, note.Automation.Keyframes[1].Value);
    }

    [Fact]
    public void PressingAnUnselectedNotesMarker_SelectsTheNote_RatherThanMovingIt()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 2, MakeInstrument(state, "boom"), 0);
        note.Automation = new AudioKeyframeManager { Gap = 1, End = 4 };
        state.SetNoteSelection([]);
        view.Layout();

        // Markers sit on the body, so an unselected note has none to catch the press.
        Assert.DoesNotContain(view.KeyframeBlocks, b => b.Note == note && b.Computed.Width > 0);

        // A press right on where the second marker is drawn selects the note instead.
        Press(ctx, view, 116, 214.5f);
        Release(ctx, view, 116, 214.5f);
        Assert.Same(note, Assert.Single(state.SelectedNotes));
        Assert.Null(note.Automation.Keyframes[1].Position);

        // Now that it is selected, its markers are handles again.
        view.InvalidateLayout();
        view.Layout();
        Assert.Contains(view.KeyframeBlocks, b => b.Note == note && b.KeyframeIndex == 1);
    }

    [Fact]
    public void CtrlDraggingAKeyframeMarker_ShapesEveryUneditedKeyframe()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 2, MakeInstrument(state, "boom"), 0);
        var automation = new AudioKeyframeManager { Gap = 1, End = 4 };
        automation.Keyframes[2].Value = new Modifier(-5); // hand-edited: the fan-out leaves it
        note.Automation = automation;
        state.SelectNote(note);
        view.Layout();

        view.WheelZooms = true; // Ctrl
        Press(ctx, view, 44 + 2 * 16 + (0.5f + 1) * 16, 214.5f);
        Drag(ctx, view, 44 + 2 * 16 + (0.5f + 1) * 16, 214.5f - 16); // two rows up
        Release(ctx, view, 44 + 2 * 16 + (0.5f + 1) * 16, 214.5f - 16);
        view.WheelZooms = false;

        Assert.Equal(new Modifier(2), automation.Template.Value);
        Assert.Equal(new Modifier(2), automation.Keyframes[0].Value);
        Assert.Equal(new Modifier(2), automation.Keyframes[1].Value);
        Assert.Equal(new Modifier(-5), automation.Keyframes[2].Value); // untouched
    }

    [Fact]
    public void HoveringANoteBorder_LightensIt_AndNothingIsDrawnAtRest()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 2, MakeInstrument(state, "boom"), 0);
        note.Automation = new AudioKeyframeManager { Gap = 4, End = 4 }; // no markers in the way
        view.Layout();

        // Hovering the body draws no handle.
        ctx.UpdatePointer(view, 76 + 30, 214.5f, false, false, false, Vector2.Zero);
        view.Update(ctx);
        Assert.Equal(0, view.ResizeHandleBand.Z);

        // Hovering the right border lightens a 6 px band on it.
        ctx.UpdatePointer(view, 76 + 4 * 16 - 3, 214.5f, false, false, false, Vector2.Zero);
        view.Update(ctx);
        Assert.Equal(6, view.ResizeHandleBand.Z);
        Assert.Equal(76 + 4 * 16 - 1 - 6, view.ResizeHandleBand.X);

        // And the pointer leaves it behind with the note.
        ctx.UpdatePointer(view, 400, 100, false, false, false, Vector2.Zero);
        view.Update(ctx);
        Assert.Equal(0, view.ResizeHandleBand.Z);
    }

    [Fact]
    public void PaintingInsideALongNote_IsANoOp_RatherThanStackingOnIt()
    {
        var (ctx, state, view, track) = NewView();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(segment, 2, boom, 0);
        note.Automation = new AudioKeyframeManager { Gap = 1, End = 4 };
        state.ActiveInstrument = boom;
        view.Layout();

        // Step 4, value 0: inside the note's span, same row.
        Click(ctx, view, 44 + 4 * 16 + 8, 214.5f);
        Assert.Same(note, Assert.Single(segment.Notes));

        // One step past its end there is room again.
        Click(ctx, view, 44 + 6 * 16 + 8, 214.5f);
        Assert.Equal(2, segment.Notes.Count);
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
        var label = view.BeatLabels.Single(l => l.Visible && l.Text == "5");
        Assert.Equal(302, label.X);
    }

    [Fact]
    public void BeatLabels_AlignToBeatBoundarySteps_NotEveryStep()
    {
        var (_, _, view, _) = NewView();
        view.Layout();

        // One segment, 4 beats of 4 steps each at 16 px/step: labels every 64 px,
        // not every 16 px step - proves beat-stride, not step-stride.
        var visible = view.BeatLabels.Where(l => l.Visible)
            .OrderBy(l => l.X).ToList();
        Assert.Equal(["1", "2", "3", "4"], visible.Select(l => l.Text).ToList());
        Assert.Equal([46f, 110f, 174f, 238f], visible.Select(l => l.X).ToList());
    }

    [Fact]
    public void BeatLabels_HighlightTheBeatContainingThePlayhead()
    {
        var (_, state, view, track) = NewView();
        state.PlaceTrack(track, 0, 0);
        // 120 BPM, 4 steps/beat: beat 2 starts at step 4 = 1 quarter note in.
        view.PlayheadQuarters = 1.0;
        view.Layout();

        var labels = view.BeatLabels.Where(l => l.Visible)
            .ToDictionary(l => l.Text);
        Assert.NotEqual(labels["1"].Color, labels["2"].Color);
    }

    [Fact]
    public void BeatLabels_ThinOutAtLowZoom_ToAvoidOverlap()
    {
        var (_, state, view, track) = NewView();
        for (var i = 0; i < 20; i++) state.AddSegment(track); // enough beats to fill the view
        view.PixelsPerStep = 4f; // min zoom: 4 px/step * 4 steps/beat = 16 px/beat, under MinBeatLabelSpacingPx
        view.Layout();

        var xs = view.BeatLabels.Where(l => l.Visible)
            .Select(l => l.X).OrderBy(x => x).ToList();
        Assert.True(xs.Count > 1);
        for (var i = 1; i < xs.Count; i++)
            Assert.True(xs[i] - xs[i - 1] >= 28f, $"labels at {xs[i - 1]} and {xs[i]} overlap");
    }

    [Fact]
    public void GutterLabels_SkipValuesBelowA10PxRow_KeepingZeroLabelled()
    {
        var (_, _, view, _) = NewView();

        view.RowHeight = 10f; // at the threshold: every value still labelled
        view.Layout();
        var xs = view.GutterLabels.Where(l => l.Visible).Select(l => l.Y).OrderBy(y => y).ToList();
        Assert.True(xs.Count > 1);
        Assert.All(Enumerable.Range(1, xs.Count - 1), i => Assert.Equal(10f, xs[i] - xs[i - 1], 3));

        view.RowHeight = 4f; // below it: every 3rd value, ~12 px apart
        view.Layout();
        var ys = view.GutterLabels.Where(l => l.Visible).Select(l => l.Y).OrderBy(y => y).ToList();
        Assert.True(ys.Count > 1);
        Assert.All(Enumerable.Range(1, ys.Count - 1), i => Assert.Equal(12f, ys[i] - ys[i - 1], 3));

        // Value 0's label survives the thinning at every zoom.
        var zero = view.GutterLabels[TrackEditorView.MaxValue];
        Assert.True(zero.Visible);
    }

    [Fact]
    public void SelectTool_MarqueeDrag_SelectsNotesInsideTheBox_AndNeverPaints()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var inside = state.AddNote(track.Segments[0], 3, boom, 0);
        var outside = state.AddNote(track.Segments[0], 10, boom, 0);
        view.Layout();
        state.ActiveTool = EditorTool.Select;

        // Box from (step 2.25, value 0.375) to (step 4.75, value -1.5): contains step 3.
        Press(ctx, view, 80, 195.5f);
        Drag(ctx, view, 120, 214.5f);
        Release(ctx, view, 120, 214.5f);
        view.Update(ctx); // marquee commit runs on capture loss

        Assert.Equal([inside], state.SelectedNotes);
        Assert.DoesNotContain(outside, state.SelectedNotes);
        Assert.Equal(2, track.Segments[0].Notes.Count); // the Select tool never paints
    }

    [Fact]
    public void SelectTool_Marquee_SelectsANote_EvenWhenTheBoxNeverReachesItsLeadingEdge()
    {
        // A note isn't a point, it's a whole rendered cell (one step wide, one row tall), so a
        // box whose numeric range never reaches the cell's leading edge (left for steps, top
        // for values, since rows are drawn top-anchored) still selects it if it overlaps at all.
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(track.Segments[0], 3, boom, 0); // cell: step [3,4), value (-1,0]
        view.Layout();
        state.ActiveTool = EditorTool.Select;

        // Anchor at (step 5, value -2) - off the note's own block entirely, so the
        // press starts a marquee instead of grabbing the note directly. Cursor at
        // (step 3.5, value -0.5) - inside the cell, short of both its left and top
        // edges (minStep=3.5 > 3, maxValue=-0.5 < 0).
        Press(ctx, view, 124, 214.5f);
        Drag(ctx, view, 100, 206.5f);
        Release(ctx, view, 100, 206.5f);
        view.Update(ctx);

        Assert.Equal([note], state.SelectedNotes);
    }

    [Fact]
    public void SelectTool_CtrlMarquee_AppendsWithoutTouchingTheExistingSelection()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var already = state.AddNote(track.Segments[0], 0, boom, 0);
        var toAdd = state.AddNote(track.Segments[0], 3, boom, 0);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetNoteSelection([already]);
        view.WheelZooms = true; // Ctrl held

        Press(ctx, view, 80, 195.5f);
        Drag(ctx, view, 120, 214.5f);
        Release(ctx, view, 120, 214.5f);
        view.Update(ctx); // marquee commit runs on capture loss

        Assert.Equal([already, toAdd], state.SelectedNotes);
    }

    [Fact]
    public void SelectTool_ShiftMarquee_RemovesContainedNotes()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(track.Segments[0], 3, boom, 0);
        var b = state.AddNote(track.Segments[0], 10, boom, 0);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetNoteSelection([a, b]);
        view.FineSnap = true; // Shift held

        Press(ctx, view, 80, 195.5f);
        Drag(ctx, view, 120, 214.5f);
        Release(ctx, view, 120, 214.5f);
        view.Update(ctx); // marquee commit runs on capture loss

        Assert.Equal([b], state.SelectedNotes);
    }

    [Fact]
    public void SelectTool_EmptyMarquee_ClearsTheSelection()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(track.Segments[0], 3, boom, 0);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetNoteSelection([note]);

        // A box far from any note.
        Press(ctx, view, 500, 100);
        Drag(ctx, view, 550, 120);
        Release(ctx, view, 550, 120);
        view.Update(ctx); // marquee commit runs on capture loss

        Assert.Empty(state.SelectedNotes);
    }

    [Fact]
    public void AutomationMarks_AreBudgetedForTheViewport_NotSpentOnScrolledOffNotes()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var segment = track.Segments[0];
        segment.Bars = 8; // 8 bars of 4/4 at 4 steps per beat = 128 steps, like a real cover's track
        // 12 marks per note (4 generated events, each a run + a value jump + a tick): the
        // first ~60 notes alone are more than the whole mark pool.
        for (var step = 0; step < 120; step++)
            state.AddNote(segment, step, boom, 0).Automation = new AudioKeyframeManager
            {
                Gap = 1,
                Template = new AudioKeyframe { Value = new Modifier(1) },
                End = 5
            };
        view.Layout();

        // Pan 20 notches right (960 px = 60 steps): everything before step 60 is off-screen.
        ctx.UpdatePointer(view, 400, 300, false, false, false, new Vector2(-20, 0));
        view.Layout();

        // The notes now on screen still get their paths - the scrolled-off ones must not
        // have eaten the pool on the way there.
        Assert.Contains(view.AutomationMarks, m => m.Z > 0 && m.X > 700);
        Assert.DoesNotContain(view.AutomationMarks, m => m.Z > 0 && m.X + m.Z < 44);
    }

    [Fact]
    public void SelectTool_Marquee_AccountsForScroll_ModelCoordinatesNotScreenPixels()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(track.Segments[0], 5, boom, 0);
        view.Layout();
        state.ActiveTool = EditorTool.Select;

        // One wheel notch (Shift+wheel pans time): ScrollX 0 -> 48 (3 steps at 16 px/step).
        view.FineSnap = true;
        ctx.UpdatePointer(view, 400, 300, false, false, false, new Vector2(0, -1));
        view.FineSnap = false; // restore: the Select tool reads FineSnap as "Shift = remove"
        view.Layout();

        // Step 5 now renders at x = 44 + 5*16 - 48 = 76 (was 124 before scrolling) - the
        // marquee must read this post-scroll position, not the pre-scroll pixel.
        Press(ctx, view, 60, 195.5f);
        Drag(ctx, view, 100, 214.5f);
        Release(ctx, view, 100, 214.5f);
        view.Update(ctx);

        Assert.Equal([note], state.SelectedNotes);
    }

    [Fact]
    public void SelectTool_ClickOnANote_ReplacesSelection_AndDraggingNeverMovesIt()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(track.Segments[0], 3, boom, 0);
        view.Layout();
        state.ActiveTool = EditorTool.Select;

        Press(ctx, view, 100, 214.5f); // the note's cell
        Drag(ctx, view, 200, 214.5f); // one step 6.25 steps right, same row
        Release(ctx, view, 200, 214.5f);

        Assert.Equal([note], state.SelectedNotes);
        Assert.Equal(3, note.Step); // the Select tool only ever selects - it never moves notes
        Assert.Equal(0, note.Value);
    }

    [Fact]
    public void SelectTool_CtrlClickOnANote_AppendsWithoutToggling()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(track.Segments[0], 3, boom, 0);
        var b = state.AddNote(track.Segments[0], 10, boom, 0);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetNoteSelection([a]);
        view.WheelZooms = true; // Ctrl held

        Click(ctx, view, 100, 214.5f); // `a` again: already selected, append is a no-op, not a toggle
        Assert.Equal([a], state.SelectedNotes);

        Click(ctx, view, 44 + 10 * 16 + 8, 214.5f); // `b`'s cell
        Assert.Equal([a, b], state.SelectedNotes);
    }

    [Fact]
    public void SelectTool_ShiftClickOnANote_RemovesIt()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(track.Segments[0], 3, boom, 0);
        var b = state.AddNote(track.Segments[0], 10, boom, 0);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetNoteSelection([a, b]);
        view.FineSnap = true; // Shift held

        Click(ctx, view, 100, 214.5f); // `a`'s cell

        Assert.Equal([b], state.SelectedNotes);
    }

    [Fact]
    public void GroupDrag_DraggingASelectedNote_UnderSelectTool_NeverMovesTheGroup()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(track.Segments[0], 3, boom, 0);
        var b = state.AddNote(track.Segments[0], 10, boom, 5);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetNoteSelection([a, b]);

        // Press `a` (step 3, value 0) and drag one step right, one value up: under the
        // Select tool this only keeps the group selected - it's the Draw tool's job to move.
        Press(ctx, view, 100, 214.5f);
        Drag(ctx, view, 116, 206.5f);
        Release(ctx, view, 116, 206.5f);

        Assert.Equal(3, a.Step);
        Assert.Equal(0, a.Value);
        Assert.Equal(10, b.Step);
        Assert.Equal(5, b.Value);
        Assert.Equal([a, b], state.SelectedNotes); // the group selection survives the (no-op) drag
    }

    [Fact]
    public void GroupDrag_DraggingASelectedNote_UnderDrawTool_MovesTheWholeGroupToo()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(track.Segments[0], 3, boom, 0);
        var b = state.AddNote(track.Segments[0], 10, boom, 5);
        view.Layout();
        state.ActiveTool = EditorTool.Select;
        state.SetNoteSelection([a, b]); // multi-select via the Select tool…
        state.ActiveTool = EditorTool.Draw; // …then switch: selection survives (§3.6)

        Press(ctx, view, 100, 214.5f);
        Drag(ctx, view, 116, 206.5f);
        Release(ctx, view, 116, 206.5f);

        Assert.Equal(4, a.Step);
        Assert.Equal(1, a.Value);
        Assert.Equal(11, b.Step);
        Assert.Equal(6, b.Value);
    }

    [Fact]
    public void GroupDrag_PressingAnUnselectedNote_ReplacesTheSelection_AndMovesOnlyIt()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(track.Segments[0], 3, boom, 0);
        var b = state.AddNote(track.Segments[0], 10, boom, 0);
        view.Layout();
        state.SetNoteSelection([a]); // `b` is not part of the selection

        Press(ctx, view, 212, 214.5f); // `b`'s cell (step 10)
        Drag(ctx, view, 228, 214.5f); // one step right
        Release(ctx, view, 228, 214.5f);

        Assert.Equal(3, a.Step); // untouched: no longer part of the (replaced) selection
        Assert.Equal(11, b.Step);
        Assert.Equal([b], state.SelectedNotes);
    }

    [Fact]
    public void GroupDrag_CollapsesTheWholeMultiFrameDrag_IntoOneUndoEntry()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(track.Segments[0], 3, boom, 0);
        var b = state.AddNote(track.Segments[0], 10, boom, 0);
        view.Layout();
        state.SetNoteSelection([a, b]);

        Press(ctx, view, 100, 214.5f); // `a`'s cell
        Drag(ctx, view, 116, 214.5f); // step 4
        Drag(ctx, view, 132, 214.5f); // step 5
        Drag(ctx, view, 148, 214.5f); // step 6
        Release(ctx, view, 148, 214.5f);

        Assert.Equal(6, a.Step);
        Assert.Equal(13, b.Step);

        // One Ctrl+Z restores BOTH notes all the way to their pre-drag positions - if the
        // three drag frames hadn't collapsed into one entry, this would only undo the
        // last frame's delta (step 6 -> 5), not the whole gesture back to step 3.
        state.Undo();
        Assert.Equal(3, a.Step);
        Assert.Equal(10, b.Step);

        state.Redo();
        Assert.Equal(6, a.Step);
        Assert.Equal(13, b.Step);
    }

    [Fact]
    public void GroupDrag_ClampsIndividualNotes_WithoutClampingTheWholeGroupsDelta()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var anchor = state.AddNote(track.Segments[0], 10, boom, 0); // plenty of room to move left
        var nearStart = state.AddNote(track.Segments[0], 2, boom, 0); // would underflow first
        view.Layout();
        state.SetNoteSelection([anchor, nearStart]);

        // Press the anchor (step 10) and drag left by 8 steps: the anchor itself stays
        // in range, but `nearStart` (step 2 - 8 = -6) must clamp to step 0 instead of
        // vanishing or capping the whole group's delta down to fit it.
        Press(ctx, view, 44 + 10 * 16 + 8, 214.5f);
        Drag(ctx, view, 44 + 2 * 16 + 8, 214.5f);
        Release(ctx, view, 44 + 2 * 16 + 8, 214.5f);

        Assert.Equal(2, anchor.Step); // the full -8 delta applied
        Assert.Equal(0, nearStart.Step); // clamped, not dropped
    }

    [Fact]
    public void DeleteKey_RemovesTheWholeMultiSelection_AsOneUndoEntry()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(track.Segments[0], 3, boom, 0);
        var b = state.AddNote(track.Segments[0], 10, boom, 0);
        view.Layout();
        Click(ctx, view, 500, 300); // focuses the view without hitting either note
        state.SetNoteSelection([a, b]);

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Delete, 0, 0, false));

        Assert.Empty(track.Segments[0].Notes);
        state.Undo();
        Assert.Equal([a, b], track.Segments[0].Notes);
    }

    [Fact]
    public void EscapeKey_ClearsTheSelection_BeforeClosingTheTrack()
    {
        var (ctx, state, view, track) = NewView();
        var note = state.AddNote(track.Segments[0], 3, MakeInstrument(state, "boom"), 0);
        view.Layout();
        Click(ctx, view, 500, 300); // focuses the view
        state.SetNoteSelection([note]);

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Escape, 0, 0, false));
        Assert.Empty(state.SelectedNotes);
        Assert.NotNull(state.OpenedTrack); // first Escape only cleared the selection

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.Escape, 0, 0, false));
        Assert.Null(state.OpenedTrack); // nothing selected: falls through to closing the track
    }

    [Fact]
    public void CtrlA_SelectsEveryNoteOfTheOpenedTrack()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(track.Segments[0], 0, boom, 0);
        var b = state.AddNote(track.Segments[0], 1, boom, 0);
        view.Layout();
        Click(ctx, view, 500, 300); // focuses the view

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.A, 0, KeyModifiers.Control, false));

        Assert.Equal([a, b], state.SelectedNotes);
    }

    [Fact]
    public void CtrlCV_CopiesAndPastesTheSelection_ThroughTheFocusedView()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(track.Segments[0], 3, boom, 5);
        view.Layout();
        Click(ctx, view, 500, 300); // focuses the view
        state.SetNoteSelection([note]);

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.C, 0, KeyModifiers.Control, false));
        state.RemoveNote(track.Segments[0], note); // clear the cell so paste-in-place doesn't collide
        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.V, 0, KeyModifiers.Control, false));

        var pasted = Assert.Single(track.Segments[0].Notes);
        Assert.Equal(3, pasted.Step);
        Assert.Equal(5, pasted.Value);
    }

    [Fact]
    public void CtrlX_CutsTheSelection()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(track.Segments[0], 3, boom, 0);
        view.Layout();
        Click(ctx, view, 500, 300); // focuses the view
        state.SetNoteSelection([note]);

        ctx.DispatchKeyDown(new KeyboardKeyEventArgs(Keys.X, 0, KeyModifiers.Control, false));

        Assert.Empty(track.Segments[0].Notes);
        state.Paste();
        Assert.Single(track.Segments[0].Notes);
    }

    [Fact]
    public void CutNotes_AlwaysRender_RegardlessOfTheActiveInstrument()
    {
        var (ctx, state, view, track) = NewView();
        var kick = MakeInstrument(state, "kick");
        var boom = MakeInstrument(state, "boom");
        var cutNote = state.AddNote(track.Segments[0], 0, kick, 0, true);
        state.ActiveInstrument = boom; // cutting kick, but boom is active - still shown
        view.InvalidateLayout();
        view.Layout();

        Assert.Contains(view.NoteBlocks, b => b.Note == cutNote);
    }

    [Fact]
    public void SimultaneousCutsOnDifferentInstruments_ShareTheStep_ButRenderInSeparateSlots()
    {
        // Individual cuts routinely fire on several sounds at once ("!cut@a|!cut@b" in a real
        // TDW file), landing two cut notes - different instruments - on the same step. Each
        // takes its own slot in the pinned row, or only one of them is ever reachable.
        var (ctx, state, view, track) = NewView();
        var kick = MakeInstrument(state, "kick");
        var snare = MakeInstrument(state, "snare");
        var kickCut = state.AddNote(track.Segments[0], 3, kick, 0, true);
        var snareCut = state.AddNote(track.Segments[0], 3, snare, 0, true);
        view.Layout();

        var kickBlock = view.NoteBlocks.Single(b => b.Note == kickCut);
        var snareBlock = view.NoteBlocks.Single(b => b.Note == snareCut);

        Assert.NotEqual(kickBlock.X.Value, snareBlock.X.Value);
        Assert.True(kickBlock.Width.Value < view.PixelsPerStep);
        Assert.True(snareBlock.Width.Value < view.PixelsPerStep);
    }

    [Fact]
    public void CutNotes_RenderInThePinnedRowAtTheBottomOfTheViewport()
    {
        var (ctx, state, view, track) = NewView();
        var kick = MakeInstrument(state, "kick");
        var cutNote = state.AddNote(track.Segments[0], 0, kick, 0, true);
        view.Layout();

        var block = view.NoteBlocks.Single(b => b.Note == cutNote);
        Assert.True(block.Y.Value >= view.Computed.Height - TrackEditorView.CutRowHeight);
        Assert.True(block.Width.Value > 0);
        Assert.True(block.Height.Value > 0);
    }

    [Fact]
    public void AGridNoteStraddlingTheGridsBottomEdge_GetsItsHeightClamped()
    {
        // Value -22's row sits at [386.5, 394.5) under this view's default centered scroll,
        // straddling GridBottom (389, just above the pinned cut row's rule). Nothing overdraws
        // the overflow, so PlaceNote has to clamp it.
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(track.Segments[0], 0, boom, -22);
        view.Layout();

        var block = view.NoteBlocks.Single(b => b.Note == note);
        Assert.Equal(2f, block.Height.Value);
    }

    [Fact]
    public void CutRowPress_PlacesACutForTheActiveInstrument()
    {
        var (ctx, state, view, track) = NewView();
        state.ActiveInstrument = MakeInstrument(state, "kick");

        // The pinned row sits at a fixed y at the bottom of the viewport - always
        // reachable without scrolling, unlike a normal value row.
        Click(ctx, view, 100, view.Computed.Height - TrackEditorView.CutRowHeight / 2);

        var note = Assert.Single(track.Segments[0].Notes);
        Assert.True(note.IsCut);
        Assert.Equal("kick", note.Instrument.Sounds.Single().Sound);
        Assert.Equal(3, note.Step);
    }

    [Fact]
    public void GroupDrag_MixedSelection_MovesBothNotesStepsTogether()
    {
        var (ctx, state, view, track) = NewView();
        var boom = MakeInstrument(state, "boom");
        var kick = MakeInstrument(state, "kick");
        var cutNote = state.AddNote(track.Segments[0], 3, kick, 0, true);
        var boomNote = state.AddNote(track.Segments[0], 3, boom, 0);
        state.SetNoteSelection([boomNote, cutNote]);
        view.Layout();

        // Press the normal note (the drag anchor) and drag one step right.
        Press(ctx, view, 100, 214.5f);
        Drag(ctx, view, 116, 214.5f);
        Release(ctx, view, 116, 214.5f);

        Assert.Equal(4, boomNote.Step);
        Assert.Equal(4, cutNote.Step); // the horizontal delta still applies to both
    }
}
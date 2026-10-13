using EditorScene.State;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarConverter.Editor;
using VisualizerScene.Settings;

namespace EditorScene.Scenes.Views;

/// <summary>
///     Input on the grid itself: wheel and middle-drag navigation, keys, painting notes under
///     the Draw tool, the marquee under Select, and seeking from the beat ruler.
/// </summary>
public partial class TrackEditorView
{
    private (TrackSegment segment, Note note)? _placing;

    /// <summary>Last pointer y of a Ctrl+middle row-scaling drag; null while none is running.</summary>
    private float? _rowScaleY;

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        _geometry.CenterPending = false;
        var pointerPx = Context.PointerX - Computed.AbsoluteX - GutterWidth;
        _geometry.Nav.Wheel(scrollDelta, WheelZooms, FineSnap, pointerPx);
        InvalidateLayout();
        return true;
    }

    /// <summary>
    ///     FL-style middle-mouse pan of both axes, fed per frame from the scene's mouse
    ///     handler (the framework only routes left/right buttons). A hold that starts
    ///     inside the view drags the viewport with the pointer until release. With Ctrl
    ///     held, the same drag scales the row height instead of panning.
    /// </summary>
    public void MiddlePan(bool held, float x, float y)
    {
        // ponytail: WheelZooms is just "Ctrl is held" - rename it if it starts lying louder.
        if (held && WheelZooms)
        {
            _geometry.Nav.MiddlePan(false, x, y, false); // cancel any pan already in progress
            if (_rowScaleY is { } last) _geometry.ScaleRows(y - Computed.AbsoluteY, y - last);
            else if (!ContainsPoint(x, y)) return;

            _rowScaleY = y;
            _geometry.CenterPending = false;
            InvalidateLayout();
            return;
        }

        _rowScaleY = null;
        if (!_geometry.Nav.MiddlePan(held, x, y, ContainsPoint(x, y))) return;
        _geometry.CenterPending = false;
        InvalidateLayout();
    }

    /// <summary>Scrolls so value 0 sits mid-viewport on the next layout.</summary>
    public void CenterOnZero()
    {
        _geometry.CenterPending = true;
        InvalidateLayout();
    }

    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        // Escape clears the selection first; only once nothing is selected does it fall
        // through to closing the track (see Editor.KeyDown for the same chain when this
        // view isn't the focused element). A chain's head isn't a shortcut, so it stays
        // out of the bind table and ahead of it.
        if (e.Key == Keys.Escape)
        {
            if (_state.SelectedNotes.Count > 0) _state.ClearSelection();
            else _state.CloseTrack();
            return true;
        }

        switch (Keybinds.Match(e, BindScene.Editor))
        {
            case Bind.EditorDelete or Bind.EditorDeleteAlt when _state.SelectedNotes.Count > 0:
                _state.DeleteSelection();
                return true;
            case Bind.EditorCopy:
                _state.CopySelection();
                return true;
            case Bind.EditorPaste:
                _state.Paste();
                return true;
            case Bind.EditorCut:
                _state.CutSelection();
                return true;
            case Bind.EditorSelectAll:
                _state.SelectAll();
                return true;
            default:
                return base.HandleKeyDown(e);
        }
    }

    /// <summary>
    ///     Pressing an empty cell places the active sound immediately (FL paint style)
    ///     and captures the pointer: holding the button and sweeping paints a note into
    ///     every new cell crossed. Presses on existing notes never reach here - their
    ///     blocks win the hit-test (and dragging those moves them instead).
    /// </summary>
    public override bool HandlePress(float x, float y)
    {
        var localX = x - Computed.AbsoluteX;
        var localY = y - Computed.AbsoluteY;
        if (localY is >= StripHeight and < GridTop)
        {
            SeekToPointer(x);
            return true;
        }

        if (localX < GutterWidth) return false;
        if (localY >= _geometry.CutRowTop) return HandleCutRowPress(x);
        if (localY < GridTop) return false;

        if (_state.ActiveTool == EditorTool.Select)
        {
            _marquee.Begin(UnsnappedStepAt(x), UnsnappedValueAt(y), FineSnap, WheelZooms);
            InvalidateLayout();
            return true; // capture: the drag updates the marquee rect
        }

        var (segment, step) = StepAt(x, false);
        if (_state.ActiveInstrument is not { } instrument || segment == null)
        {
            _state.SelectNote(null);
            return false;
        }

        if (Paint(segment, step, instrument, ValueAt(y)) is not { } painted) return false;
        _placing = painted;
        return true; // capture: the sweep keeps painting
    }

    /// <summary>
    ///     A press in the pinned cut row places/starts sweeping a cut targeting whichever
    ///     instrument is active - any instrument can be cut, there's no reserved one.
    /// </summary>
    private bool HandleCutRowPress(float x)
    {
        var (segment, step) = StepAt(x, false);
        if (_state.ActiveInstrument is not { } instrument || segment == null) return false;

        if (Paint(segment, step, instrument, 0, true) is not { } painted) return false;
        _placing = painted;
        return true; // capture: the sweep keeps painting
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (_marquee.Active)
        {
            _marquee.Drag(UnsnappedStepAt(x), UnsnappedValueAt(y));
            InvalidateLayout();
            return;
        }

        if (_placing is not ({ } lastSegment, { } lastNote)) return;
        var (segment, step) = StepAt(x, true);
        if (segment == null) return;

        if (lastNote.IsCut)
        {
            if (segment == lastSegment && step == lastNote.Step) return; // same cell
            if (Paint(segment, step, lastNote.Instrument, 0, true) is { } cutPainted) _placing = cutPainted;
            return;
        }

        var value = ValueAt(y);
        if (segment == lastSegment && step == lastNote.Step && value == lastNote.Value) return; // same cell

        if (Paint(segment, step, lastNote.Instrument, value) is { } painted) _placing = painted;
    }

    /// <summary>
    ///     Applies the marquee's Replace/Append/Remove modifier semantics against every note
    ///     whose (global step, value) falls inside the box - the note itself, not its
    ///     rendered block, so pooled-out notes at chart-dense zoom are never missed.
    /// </summary>
    private void CommitMarquee()
    {
        if (_state.OpenedTrack is not { } track) return;
        var (minStep, maxStep, minValue, maxValue) = _marquee.Box;

        var contained = new List<Note>();
        var offset = 0;
        foreach (var segment in track.Segments)
        {
            foreach (var note in segment.Notes)
            {
                // Cut notes render in the pinned row, outside the scrollable grid the
                // marquee draws in - never reachable by it, regardless of their (unused) Value.
                if (note.IsCut) continue;

                // A note isn't a point - it's a whole rendered cell, one step wide
                // (globalStep .. globalStep+1) and one row tall. The row is drawn
                // top-anchored (ValueTop), so its cell spans (Value-1 .. Value], the
                // opposite orientation from the step axis. The marquee is tested
                // against that cell, so a box drawn entirely inside one still
                // selects the note.
                var globalStep = offset + note.Step;
                var stepOverlaps = globalStep < maxStep && globalStep + NoteSteps(track, segment, note) > minStep;
                var valueOverlaps = note.Value - 1 < maxValue && note.Value > minValue;
                if (stepOverlaps && valueOverlaps) contained.Add(note);
            }

            offset += segment.StepCount;
        }

        _marquee.Apply(contained, _state.SetNoteSelection, _state.AddToNoteSelection, _state.RemoveFromNoteSelection);
    }

    /// <summary>
    ///     Makes a note's instrument the one the next click paints - the eyedropper a piano
    ///     roll is expected to have, and the only way to get back to an instrument a track
    ///     plays without walking the picker. Cuts included: a cut targets an instrument like
    ///     any other note. Fires the instruments event by hand, since a plain
    ///     <see cref="EditorState.ActiveInstrument" /> set raises none and the tool bar's
    ///     button reads it.
    /// </summary>
    internal void PickInstrument(Note note)
    {
        if (_state.ActiveInstrument == note.Instrument) return;

        _state.ActiveInstrument = note.Instrument;
        _state.NotifyInstrumentsChanged();
    }

    /// <summary>
    ///     Places one painted note, unless the cell already holds an identical one. A cut's
    ///     dedup is per (step, instrument) - independent instruments' cuts may share a step,
    ///     but re-pressing the same instrument's cut on the same step is a no-op, not a stack.
    /// </summary>
    private (TrackSegment segment, Note note)? Paint(TrackSegment segment, int step, Instrument instrument,
        double value, bool isCut = false)
    {
        var track = _state.OpenedTrack;
        // A long note occupies every step it covers, so a paint sweep can't stack a note
        // inside one that is already there.
        var duplicate = isCut
            ? segment.Notes.Any(n => n.Step == step && n.IsCut && n.Instrument == instrument)
            : segment.Notes.Any(n => !n.IsCut && n.Value == value && n.Step <= step &&
                                     n.Step + (track is null ? 1 : NoteSteps(track, segment, n)) > step);
        if (duplicate) return null;

        var note = _state.AddNote(segment, step, instrument, value, isCut);
        _state.SelectSegment(segment);
        _state.SelectNote(note);
        OnPreviewNote?.Invoke(note, null);
        InvalidateLayout();
        return (segment, note);
    }

    /// <summary>
    ///     Converts a ruler click's absolute x into an arrangement-timeline seek: the track can
    ///     be placed on the arrangement more than once, so this seeks the occurrence nearest the
    ///     current playhead rather than guessing "first".
    /// </summary>
    private void SeekToPointer(float absX)
    {
        if (_state.OpenedTrack is not { } track || OnSeekQuarters == null) return;

        var steps = Math.Max(0, (absX - Computed.AbsoluteX - GutterWidth + _geometry.ScrollX) / PixelsPerStep);
        var localMinutes = track.MinutesAtStepPosition(steps);

        var placements = _state.Project.Placements.Where(p => p.Track == track).ToArray();
        if (placements.Length == 0) return;
        var placement = placements.Length == 1
            ? placements[0]
            : placements.MinBy(p => Math.Abs(p.StartQuarterNotes - PlayheadQuarters))!;

        OnSeekQuarters.Invoke(placement.StartQuarterNotes + localMinutes * _state.Project.RootTiming.BPM);
    }
}

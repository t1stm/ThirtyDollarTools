using EditorScene.State;
using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Views;

/// <summary>
///     The drags a pooled block starts: moving one note or the whole selection, resizing a
///     note by its border, and shaping an automation by its keyframe markers.
/// </summary>
public partial class TrackEditorView
{
    /// <summary>Which border a running resize drag has hold of: -1 left, +1 right, 0 = none.</summary>
    private int _resizeEdge;

    /// <summary>The marker a keyframe drag has hold of; null while none is running.</summary>
    private KeyframeBlock? _keyframeDrag;

    /// <summary>Indices that carried their own settings when the drag started (Ctrl fan-out).</summary>
    private HashSet<int> _keyframeEdited = [];

    /// <summary>Whether the running keyframe drag shapes the whole automation (Ctrl held).</summary>
    private bool _keyframeFanOut;
    private List<GroupDragEntry>? _groupDrag;
    private int _groupDragAnchorStartStep;
    private double _groupDragAnchorStartValue;
    private int _groupDragLastStep;
    private double _groupDragLastValue;

    /// <summary>
    ///     Starts a marker drag. Ctrl at press makes it shape the whole automation: the value
    ///     goes into the template and fans out to every keyframe that had no settings of its
    ///     own, which is captured here so the fan-out cannot swallow hand-edited keyframes as
    ///     the template moves under them.
    /// </summary>
    internal void BeginKeyframeDrag(KeyframeBlock block)
    {
        if (block.Note?.Automation is not { } automation) return;

        _keyframeDrag = block;
        _keyframeFanOut = WheelZooms;
        _keyframeEdited =
        [
            .. Enumerable.Range(0, automation.Keyframes.Count)
                .Where(i => !automation.Keyframes[i].ValueEquals(automation.Template))
        ];
        _state.BeginGesture();
    }

    /// <summary>
    ///     One frame of a marker drag: vertically it writes the keyframe's value, horizontally
    ///     its own position. Modifiers are relative, so the value is written as
    ///     "add whatever gets from the previous keyframe's result to the pointer" - always
    ///     defined, which a multiplicative one would not be. The position is clamped strictly
    ///     between the neighbouring markers, so markers can never cross or reorder.
    /// </summary>
    internal void UpdateKeyframeDrag(float x, float y)
    {
        if (_keyframeDrag is not { Note: { Automation: { } automation } note, Segment: { } segment } block) return;
        if (_state.OpenedTrack is not { } track) return;

        var index = block.KeyframeIndex;
        if (index >= automation.Keyframes.Count) return;

        var stepMinutes = segment.StepMinutes(track.Timing.BPM);
        var noteGlobalStep = track.GlobalStepOf(segment, note.Step);
        // Markers are drawn in the middle of the cell their position falls in, like the
        // note's own start is - so the pointer's own half-step offset comes back off here.
        var steps = UnsnappedStepAt(x) - noteGlobalStep - 0.5;
        var position = automation.Timing == KeyframeTiming.Step ? steps : steps * stepMinutes * 60d;

        // Strictly inside the neighbours, and strictly inside the note.
        const float margin = 0.001f;
        var lower = index > 0 ? automation.PositionOf(index - 1) : 0;
        var upper = index + 1 < automation.Keyframes.Count ? automation.PositionOf(index + 1) : automation.End;
        var clamped = Math.Clamp((float)position, lower + margin, Math.Max(lower + margin, upper - margin));

        var target = Math.Clamp(ValueAt(y), -MaxValue, MaxValue);
        var modifier = new Modifier(target - automation.ValueBefore(note, index));

        _state.EditAutomation(automation, () =>
        {
            automation.Keyframes[index].Position = clamped;
            if (!_keyframeFanOut)
            {
                automation.Keyframes[index].Value = modifier;
                return;
            }

            automation.Template.Value = modifier;
            for (var i = 0; i < automation.Keyframes.Count; i++)
                if (!_keyframeEdited.Contains(i))
                    automation.Keyframes[i].Value = modifier;
        });

        InvalidateLayout();
    }

    /// <summary>
    ///     Starts a group drag on the given note block: captures every currently
    ///     selected note's starting (global step, value) - the block's own note is one
    ///     of them (a fresh press onto an unselected note already replaced the selection
    ///     with just it, so this naturally degrades to a plain single-note drag).
    /// </summary>
    internal void BeginNoteDrag(NoteBlock block, float pressX, float pressY)
    {
        if (_state.OpenedTrack is not { } track || block.Note == null || block.Segment == null) return;

        _dragging = block;
        _state.BeginGesture();

        // The pointer's own step at press, not the note's: the drag moves every note by the
        // delta from here, so anchoring on the note's start would shift it by however far
        // into the note the press landed - grabbing a long note in the middle would snap its
        // start to the cursor before the pointer moved at all.
        var layout = new TrackLayout(track);
        var (pressSegment, pressStep) = StepAt(pressX, true);
        var anchorGlobalStep = pressSegment != null
            ? layout.GlobalStepOf(pressSegment, pressStep)
            : layout.GlobalStepOf(block.Segment, block.Note.Step);
        _groupDragAnchorStartStep = anchorGlobalStep;
        // Snapped pointer value at press, not the note's own exact Value, so both ends of
        // the per-frame delta are snapped the same way. A same-position drag frame - which
        // the input dispatcher fires for a plain held click (see UIContext.UpdatePointer) -
        // then yields a delta of exactly zero, and a one-row drag a clean whole number. That
        // delta adds onto each note's own exact StartValue below, so a fractional value
        // (6.4, which only snaps back to itself in FineSnap mode) survives the drag instead
        // of being silently rounded.
        _groupDragAnchorStartValue = ValueAt(pressY);
        _groupDragLastStep = anchorGlobalStep;
        _groupDragLastValue = _groupDragAnchorStartValue;

        _groupDrag =
        [
            .. _state.SelectedNotes.Select(note =>
            {
                var segment = layout.SegmentOf(note);
                var globalStep = segment != null ? layout.GlobalStepOf(segment, note.Step) : note.Step;
                return new GroupDragEntry(note, globalStep, note.Value);
            })
        ];
    }

    /// <summary>
    ///     Lightens the border band under the pointer, so the grab zone shows itself without
    ///     anything being drawn on a note at rest. Written per frame rather than per layout:
    ///     hovering a border changes no geometry, so nothing would invalidate the layout.
    /// </summary>
    private void DrawResizeHandle(UIContext uiContext)
    {
        var hovered = _dragging;
        if (hovered == null)
            foreach (var block in _noteBlocks)
                if (block is { IsHovered: true, Note: not null })
                {
                    hovered = block;
                    break;
                }

        var edge = _resizeEdge != 0 ? _resizeEdge : hovered?.EdgeAt(uiContext.PointerX) ?? 0;
        if (hovered?.Note == null || edge == 0)
        {
            _blockBatch.Hide(ResizeHandleSlot);
            _handleBand = default;
            return;
        }

        var zone = hovered.EdgeZone;
        var width = hovered.Computed.Width;
        var x = hovered.Computed.AbsoluteX + (edge > 0 ? width - zone : 0);
        var y = hovered.Computed.AbsoluteY;
        var height = hovered.Computed.Height;

        // The band is the note's own fill, lifted toward white - it reads as the same note.
        _blockBatch.Set(ResizeHandleSlot, x, y, zone, height,
            Lighten(_blockBatch.ColorOf(hovered.BatchSlot), 0.35f));
        _handleBand = new Vector4(x - Computed.AbsoluteX, y - Computed.AbsoluteY, zone, height);
    }

    /// <summary>
    ///     Starts a border drag on the given block's edge: -1 is its left border, +1 its
    ///     right. Every selected note is captured with its own start and length, so a group
    ///     resize applies one delta to all of them - the same shape as the group move, and
    ///     the same single undo entry.
    /// </summary>
    internal void BeginNoteResize(NoteBlock block, int edge, float pressX)
    {
        if (_state.OpenedTrack is not { } track || block.Note == null || block.Segment == null) return;

        _dragging = block;
        _resizeEdge = edge;
        _state.BeginGesture();

        var layout = new TrackLayout(track);
        var (_, pressStep) = StepAt(pressX, true);
        _groupDragAnchorStartStep = layout.GlobalStepOf(block.Segment, pressStep);
        _groupDragLastStep = _groupDragAnchorStartStep;

        _groupDrag =
        [
            .. _state.SelectedNotes.Select(note =>
            {
                var segment = layout.SegmentOf(note);
                var globalStep = segment != null ? layout.GlobalStepOf(segment, note.Step) : note.Step;
                var steps = segment != null ? NoteSteps(track, segment, note) : 1;
                return new GroupDragEntry(note, globalStep, note.Value, steps);
            })
        ];
    }

    /// <summary>
    ///     One frame of a border drag. The right border sets the length; the left one moves
    ///     the note and grows the length by as much, so its end stays put. Both are floored
    ///     at one step and clamped to the track's own length, and a note with no automation
    ///     gets one the moment it is dragged - a cutting sustain with no gap, so it holds for
    ///     its whole length until a gap is typed into the inspector.
    /// </summary>
    private void UpdateResizeDrag(float x)
    {
        if (_groupDrag is not { Count: > 0 } entries || _state.OpenedTrack is not { } track) return;

        var (segment, step) = StepAt(x, true);
        if (segment == null) return;

        var layout = new TrackLayout(track);
        var totalSteps = layout.TotalSteps;
        var newAnchorGlobalStep = layout.GlobalStepOf(segment, step);
        var stepDelta = newAnchorGlobalStep - _groupDragAnchorStartStep;

        var targets = new List<(Note Note, TrackSegment Segment, int Step, double Value, float? End)>(entries.Count);
        foreach (var entry in entries)
        {
            var globalStep = entry.StartGlobalStep;
            var end = entry.StartSteps;
            if (_resizeEdge > 0)
            {
                end = Math.Clamp(end + stepDelta, 1, Math.Max(1, totalSteps - globalStep));
            }
            else
            {
                // The end stays put: whatever the start loses, the length gains.
                var moved = Math.Clamp(globalStep + stepDelta, 0,
                    globalStep + (int)Math.Ceiling(entry.StartSteps) - 1);
                end = globalStep + entry.StartSteps - moved;
                globalStep = moved;
            }

            if (layout.SegmentAt(globalStep) is not { } mapped) continue;
            targets.Add((entry.Note, mapped.Segment, mapped.LocalStep, entry.Note.Value, end));
        }

        _state.MoveSelectedNotes(track, targets);

        foreach (var target in targets)
            if (target.Note == _dragging?.Note)
            {
                _dragging!.Segment = target.Segment;
                break;
            }

        _groupDragLastStep = newAnchorGlobalStep;
        InvalidateLayout();
    }

    /// <summary>Routes a drag frame to the gesture that is running - a resize or a move.</summary>
    internal void UpdateDrag(float x, float y)
    {
        if (_resizeEdge != 0) UpdateResizeDrag(x);
        else UpdateGroupDrag(x, y);
    }

    /// <summary>
    ///     Applies the anchor's per-frame delta (from its own drag start) to every
    ///     selected note's own start, so the whole group moves together; a group of one
    ///     reduces to a plain single-note drag. Steps/values are clamped into the track's
    ///     valid range per note (matching FL: notes at the edge just stop there, which can
    ///     compress the group's relative spacing at the boundary).
    /// </summary>
    internal void UpdateGroupDrag(float x, float y)
    {
        if (_groupDrag is not { Count: > 0 } entries || _state.OpenedTrack is not { } track) return;

        var (segment, step) = StepAt(x, true);
        if (segment == null) return;
        var value = ValueAt(y);

        var layout = new TrackLayout(track);
        var newAnchorGlobalStep = layout.GlobalStepOf(segment, step);
        var stepDelta = newAnchorGlobalStep - _groupDragAnchorStartStep;
        var valueDelta = value - _groupDragAnchorStartValue;

        var maxGlobalStep = Math.Max(0, layout.TotalSteps - 1);
        var targets = new List<(Note Note, TrackSegment Segment, int Step, double Value, float? End)>(entries.Count);
        foreach (var entry in entries)
        {
            var targetGlobalStep = Math.Clamp(entry.StartGlobalStep + stepDelta, 0, maxGlobalStep);
            // A cut note's Value is unused (it renders in the fixed pinned row regardless),
            // so clamping it along with everything else is harmless - no special case needed.
            var targetValue = Math.Clamp(entry.StartValue + valueDelta, -MaxValue, MaxValue);
            if (layout.SegmentAt(targetGlobalStep) is not { } mapped) continue;

            targets.Add((entry.Note, mapped.Segment, mapped.LocalStep, targetValue, entry.Note.Automation?.End));
        }

        _state.MoveSelectedNotes(track, targets);

        // The pinned block's own Segment must track its note across a boundary crossing.
        foreach (var target in targets)
            if (target.Note == _dragging?.Note)
            {
                _dragging!.Segment = target.Segment;
                break;
            }

        var moved = newAnchorGlobalStep != _groupDragLastStep || value != _groupDragLastValue;
        _groupDragLastStep = newAnchorGlobalStep;
        _groupDragLastValue = value;
        InvalidateLayout();
        if (moved) OnPreviewNote?.Invoke(_dragging!.Note!, value);
    }

    // Group note drag: every selected note's starting (global step, value), captured
    // once at press. Each drag frame re-derives the anchor's (pressed note's) delta
    // from its own start and applies that same delta to every entry - this is what
    // makes dragging one note of a multi-selection move the whole group together.
    private readonly record struct GroupDragEntry(Note Note, int StartGlobalStep, double StartValue,
        float StartSteps = 1);
}

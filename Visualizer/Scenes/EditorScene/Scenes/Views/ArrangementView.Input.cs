using EditorScene.State;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ThirtyDollarConverter.Editor;
using VisualizerScene.Settings;

namespace EditorScene.Scenes.Views;

/// <summary>
///     Input on the lanes: wheel and middle-drag navigation, keys, placing a clip under the
///     Draw tool, the marquee under Select, and dragging the selected clips.
/// </summary>
public partial class ArrangementView
{
    private ClipBlock? _dragging;

    // A clip drag moves the whole selection: every selected clip's position at press,
    // plus the anchor's own snapped press position, so each frame applies the anchor's
    // delta to the originals (see UpdateClipDrag). Null while no drag is running.
    private List<(TrackPlacement Placement, int Channel, double Start)>? _clipDrag;

    private (int Channel, double Start) _clipDragAnchor;

    /// <summary>Continuous, unsnapped quarter-note position - the marquee's counterpart to <see cref="GridPosition" />.</summary>
    private double UnsnappedQuartersAt(float localX)
    {
        return (localX + _nav.ScrollX) / PixelsPerQuarter;
    }

    /// <summary>Continuous, unsnapped channel - the marquee's counterpart to <see cref="GridPosition" />.</summary>
    private double UnsnappedChannelAt(float localY)
    {
        return (localY - RulerHeight + _nav.ScrollY) / LaneHeight;
    }

    /// <summary>
    ///     Select tool only: starts a marquee on an empty-lane press (below the ruler).
    ///     Empty-lane presses otherwise reach only <c>OnClick</c> on release, but the
    ///     marquee needs the press to capture the pointer for the drag.
    /// </summary>
    public override bool HandlePress(float x, float y)
    {
        var localY = y - Computed.AbsoluteY;
        if (localY < RulerHeight) return false; // ruler seek still runs on release via OnClick
        if (_state.ActiveTool != EditorTool.Select) return false; // Draw tool: PlaceAtPointer on release, unchanged

        var localX = x - Computed.AbsoluteX;
        _marquee.Begin(UnsnappedQuartersAt(localX), UnsnappedChannelAt(localY), FineSnap, WheelZooms);
        InvalidateLayout();
        return true; // capture: the drag updates the marquee rect
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (!_marquee.Active) return;
        _marquee.Drag(UnsnappedQuartersAt(x - Computed.AbsoluteX), UnsnappedChannelAt(y - Computed.AbsoluteY));
        InvalidateLayout();
    }

    /// <summary>
    ///     Applies the marquee's Replace/Append/Remove modifier semantics against every
    ///     placement whose time span intersects the marquee's quarter range and whose
    ///     channel is within its row range - span-intersection (not containment), since
    ///     clips are wide objects and a marquee merely touching one should select it.
    /// </summary>
    private void CommitMarquee()
    {
        var (minQ, maxQ, minChannel, maxChannel) = _marquee.Box;

        var contained = new List<TrackPlacement>();
        foreach (var placement in _state.Project.Placements)
        {
            var quarters = placement.Track.DurationMinutes() * _state.Project.RootTiming.BPM;
            var end = placement.StartQuarterNotes + quarters;
            if (end <= minQ || placement.StartQuarterNotes >= maxQ) continue;

            // The lane is a whole rendered row (Channel .. Channel+1), not a point, so
            // the marquee's box is tested against that span - a box drawn entirely
            // inside a row still selects it, as on the time axis above.
            if (placement.Channel >= maxChannel || placement.Channel + 1 <= minChannel) continue;

            contained.Add(placement);
        }

        _marquee.Apply(contained, _state.SetPlacementSelection, _state.AddToPlacementSelection,
            _state.RemoveFromPlacementSelection);
    }

    /// <summary>
    ///     Plain wheel scrolls lanes vertically; Shift+wheel pans time instead -
    ///     same binding as <see cref="TrackEditorView.HandleScroll" />'s FineSnap/panXWithY.
    /// </summary>
    public override bool HandleScroll(Vector2 scrollDelta)
    {
        var pointerPx = Context.PointerX - Computed.AbsoluteX;
        _nav.Wheel(scrollDelta, WheelZooms, FineSnap, pointerPx);
        InvalidateLayout();
        OnScrolled?.Invoke();
        return true;
    }

    /// <summary>
    ///     FL-style middle-mouse pan of both axes, fed per frame from the scene's mouse
    ///     handler (the framework only routes left/right buttons). A hold that starts
    ///     inside the view drags the viewport with the pointer until release. With Ctrl
    ///     held, the same drag scales the lane height instead, as in the note editor.
    /// </summary>
    public void MiddlePan(bool held, float x, float y)
    {
        var rowPointerPx = y - Computed.AbsoluteY - RulerHeight;
        if (!_nav.MiddleDrag(held, WheelZooms, x, y, rowPointerPx, ContainsPoint(x, y))) return;
        InvalidateLayout();
        OnScrolled?.Invoke();
    }

    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        switch (Keybinds.Match(e, BindScene.Editor))
        {
            case Bind.EditorDelete or Bind.EditorDeleteAlt when _state.SelectedPlacements.Count > 0:
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
        }

        // Escape is the fallthrough chain's first step, not a shortcut - see Editor.KeyDown.
        if (e.Key != Keys.Escape || _state.SelectedPlacements.Count == 0) return base.HandleKeyDown(e);

        _state.ClearSelection();
        return true;
    }

    private void PlaceAtPointer()
    {
        if (_state.SelectedTrack is not { } track)
        {
            _state.SelectPlacement(null);
            return;
        }

        var (channel, start) = GridPosition(Context.PointerX, Context.PointerY, 0);
        _state.SelectPlacement(_state.PlaceTrack(track, channel, start));
    }

    /// <summary>
    ///     Captures every selected clip's position at press, along with the anchor clip's own
    ///     snapped press position - the clip under the pointer is one of them, since a press
    ///     onto an unselected clip has already replaced the selection with just it.
    /// </summary>
    private void BeginClipDrag(float x, float y, double grabOffsetQuarters)
    {
        _clipDragAnchor = GridPosition(x, y, grabOffsetQuarters);
        _clipDrag = [.. _state.SelectedPlacements.Select(p => (p, p.Channel, p.StartQuarterNotes))];
    }

    /// <summary>
    ///     One frame of a clip drag: the anchor's delta from its own press position applies
    ///     to every captured clip's own start and channel, so the group keeps its shape and a
    ///     selection of one reduces to a plain single-clip drag. Clamping is per clip, as in
    ///     <see cref="TrackEditorView.UpdateGroupDrag" /> - a clip against the timeline start
    ///     or the last lane stops there while the rest keep moving.
    /// </summary>
    private void UpdateClipDrag(float x, float y, double grabOffsetQuarters)
    {
        if (_clipDrag is not { Count: > 0 } entries) return;

        var (channel, start) = GridPosition(x, y, grabOffsetQuarters);
        var channelDelta = channel - _clipDragAnchor.Channel;
        var startDelta = start - _clipDragAnchor.Start;
        var maxChannel = ChannelCount - 1;

        _state.MovePlacements([
            .. entries.Select(e => (e.Placement,
                Math.Clamp(e.Channel + channelDelta, 0, maxChannel),
                Math.Max(0, e.Start + startDelta)))
        ]);
        InvalidateLayout();
    }

    private (int channel, double start) GridPosition(float x, float y, double grabOffsetQuarters)
    {
        var quarters = (x - Computed.AbsoluteX + _nav.ScrollX) / PixelsPerQuarter - grabOffsetQuarters;
        var snapped = Math.Max(0, Math.Round(quarters / SnapQuarterNotes) * SnapQuarterNotes);
        var channel = (int)Math.Floor((y - Computed.AbsoluteY - RulerHeight + _nav.ScrollY) / LaneHeight);
        return (Math.Clamp(channel, 0, ChannelCount - 1), snapped);
    }
}

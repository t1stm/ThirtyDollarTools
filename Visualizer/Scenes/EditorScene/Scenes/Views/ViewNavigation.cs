using OpenTK.Mathematics;

namespace EditorScene.Scenes.Views;

/// <summary>
///     Shared scroll/zoom/middle-pan math for <see cref="TrackEditorGeometry" /> (via
///     composition) and <see cref="ArrangementView" />. Plain state, no GL.
///     <see cref="MaxScrollY" /> decides whether vertical scroll does anything: the note
///     editor sets it from its viewport every layout, the arrangement leaves it 0 so a
///     diagonal middle-drag pans time only. <see cref="RowHeight" /> is the vertical zoom:
///     the note editor's value rows, the arrangement's lanes.
/// </summary>
public sealed class ViewNavigation(float minZoom, float maxZoom, float minRowHeight, float maxRowHeight)
{
    private Vector2? _panPointer;

    /// <summary>Last pointer y of a Ctrl+middle row-scaling drag; null while none is running.</summary>
    private float? _rowScaleY;

    public float ScrollX { get; set; }
    public float ScrollY { get; set; }
    public float Zoom { get; set; }
    public float RowHeight { get; set; }
    public float MaxScrollY { get; set; }

    /// <summary>
    ///     Horizontally, only the left edge is bounded - content can scroll past the
    ///     viewport on the right (lets playhead-follow keep centering to the very end of
    ///     playback). Vertically, bounded to <see cref="MaxScrollY" />, which is 0 for views
    ///     with no vertical scroll.
    /// </summary>
    public void Clamp()
    {
        ScrollX = Math.Max(0, ScrollX);
        ScrollY = Math.Clamp(ScrollY, 0, MaxScrollY);
    }

    /// <summary>
    ///     Pointer-anchored zoom (Ctrl+wheel): the unit under the cursor stays put.
    ///     <paramref name="pointerPx" /> is local, already offset past any gutter/header.
    /// </summary>
    public void ZoomAt(float pointerPx, float wheelDeltaY)
    {
        var anchorUnits = (pointerPx + ScrollX) / Zoom;
        Zoom = Math.Clamp(Zoom * MathF.Pow(1.15f, wheelDeltaY), minZoom, maxZoom);
        ScrollX = anchorUnits * Zoom - pointerPx;
    }

    /// <summary>
    ///     Pointer-anchored vertical scaling (Ctrl+middle drag): the row under the cursor
    ///     stays put while the rows grow or shrink. <paramref name="pointerPx" /> is local,
    ///     already offset past any ruler; <paramref name="dy" /> is the pointer's movement
    ///     since the last call - negative (upwards) grows the rows. Only the top is clamped
    ///     here: the bottom bound depends on the new height, so the view's next layout
    ///     recomputes <see cref="MaxScrollY" /> first.
    /// </summary>
    public void ScaleRowsAt(float pointerPx, float dy)
    {
        var anchorRows = (pointerPx + ScrollY) / RowHeight;
        RowHeight = Math.Clamp(RowHeight * MathF.Pow(1.01f, -dy), minRowHeight, maxRowHeight);
        ScrollY = Math.Max(0, anchorRows * RowHeight - pointerPx);
    }

    /// <summary>
    ///     One wheel event. <paramref name="zoom" /> zooms at the pointer;
    ///     otherwise <paramref name="panXWithY" /> pans time with the wheel's plain axis
    ///     (the note editor's Shift/fine-snap binding, the arrangement's only mode), else the
    ///     plain wheel scrolls Y and a tilt wheel/touchpad X pans time.
    /// </summary>
    public void Wheel(Vector2 delta, bool zoom, bool panXWithY, float pointerPx)
    {
        if (zoom)
        {
            ZoomAt(pointerPx, delta.Y);
        }
        else if (panXWithY)
        {
            ScrollX -= delta.Y * 48f;
        }
        else
        {
            ScrollY -= delta.Y * 48f;
            ScrollX -= delta.X * 48f;
        }

        Clamp();
    }

    /// <summary>
    ///     FL-style middle-mouse pan of both axes, fed per frame from the scene's mouse
    ///     handler (the framework only routes left/right buttons). A hold that starts
    ///     inside the view (<paramref name="startAllowed" />) drags the viewport with the
    ///     pointer until release. Returns whether the viewport actually moved this call, so
    ///     the caller knows whether to invalidate layout.
    /// </summary>
    public bool MiddlePan(bool held, float x, float y, bool startAllowed)
    {
        if (!held)
        {
            _panPointer = null;
            return false;
        }

        if (_panPointer is { } last)
        {
            ScrollX += last.X - x;
            ScrollY += last.Y - y;
            _panPointer = new Vector2(x, y);
            Clamp();
            return true;
        }

        if (startAllowed) _panPointer = new Vector2(x, y);
        return false;
    }

    /// <summary>
    ///     The middle button's per-frame feed: a plain hold pans (see <see cref="MiddlePan" />);
    ///     with <paramref name="scaleRows" /> (Ctrl) held, the same drag scales the row height
    ///     instead (see <see cref="ScaleRowsAt" />, which <paramref name="rowPointerPx" /> is
    ///     passed to). Returns whether the viewport may have changed this call.
    /// </summary>
    public bool MiddleDrag(bool held, bool scaleRows, float x, float y, float rowPointerPx, bool startAllowed)
    {
        if (held && scaleRows)
        {
            _panPointer = null; // cancel any pan already in progress
            if (_rowScaleY is { } last) ScaleRowsAt(rowPointerPx, y - last);
            else if (!startAllowed) return false;

            _rowScaleY = y;
            return true;
        }

        _rowScaleY = null;
        return MiddlePan(held, x, y, startAllowed);
    }
}
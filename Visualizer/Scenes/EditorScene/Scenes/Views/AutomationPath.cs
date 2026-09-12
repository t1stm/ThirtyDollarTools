using OpenTK.Mathematics;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Views;

/// <summary>
///     Plots a note's generated automation events over its body, in the note's sound color:
///     a tick at every generated event and a straight line leaning from each one to the
///     next, so the path shows where the value is going instead of stepping horizontally and
///     then jumping. A bright cap on the note's right edge marks where a
///     <see cref="AudioKeyframeManager.CutAtEnd" /> stops the note, so one that ends reads
///     apart from one that bleeds into what follows. Time-mode positions are mapped through
///     the note's own segment step rate, a display-only approximation once the path crosses
///     into a segment with another tempo. Draws into a slot range of
///     <see cref="TrackEditorView" />'s block batch rather than owning elements - the path
///     takes no input - sitting after the note pool so the marks paint over the bodies.
///     Marks outside the viewport are dropped without taking a slot, so a 256-keyframe note
///     costs what is on screen; past <paramref name="cap" /> the path simply stops drawing.
/// </summary>
internal sealed class AutomationPath(LineBatch batch, int firstSlot, int cap)
{
    private readonly List<Vector4> _marks = [];
    private readonly List<MarkerHandle> _handles = [];
    private float _clipRight;
    private Vector2 _origin;

    /// <summary>
    ///     Where the last layout put every visible keyframe marker, in view-local
    ///     coordinates - what the view hangs its draggable <see cref="KeyframeBlock" />
    ///     pool on. Rebuilt per layout, like the marks themselves.
    /// </summary>
    public IReadOnlyList<MarkerHandle> Handles => _handles;

    /// <summary>One visible marker: which keyframe it is, and where it was drawn.</summary>
    internal readonly record struct MarkerHandle(Note Note, TrackSegment Segment, int Index, float X, float Y);

    /// <summary>Clears the handle list; the view calls this once before the segment pass.</summary>
    public void BeginFrame()
    {
        _handles.Clear();
    }

    /// <summary>
    ///     Test seam: what the last layout drew, in view-local coordinates. A tick or cap is
    ///     (x, y, width, height); a connecting line is (x, y, length, thickness) from its
    ///     left-hand end, since a leaning line has no axis-aligned rect to report.
    /// </summary>
    public IReadOnlyList<Vector4> Marks => _marks;

    /// <summary>
    ///     Releases every slot from <paramref name="used" /> onward - this layout's unused
    ///     tail. The mark list is the high-water mark to release back to, since the same
    ///     writes fill both it and the batch.
    /// </summary>
    public void HideUnused(int used)
    {
        for (var i = used; i < _marks.Count; i++) batch.Hide(firstSlot + i);
        if (_marks.Count > used) _marks.RemoveRange(used, _marks.Count - used);
    }

    public void Draw(TrackEditorGeometry geometry, Vector2 origin, ProjectTrack track, TrackSegment segment, Note note,
        float segStartPx, Vector4 color, Vector4 endColor, ref int used)
    {
        var automation = note.Automation!;
        var stepMinutes = segment.StepMinutes(track.Timing.BPM);
        if (stepMinutes <= 0) return;

        var maxY = geometry.GridBottom;
        _clipRight = geometry.ViewWidth;
        _origin = origin;
        var pixelsPerStep = geometry.PixelsPerStep;
        var scrollX = geometry.ScrollX;
        var rowHeight = geometry.RowHeight;
        var noteX = TrackEditorGeometry.GutterWidth + segStartPx + note.Step * pixelsPerStep - scrollX;
        var prevX = noteX + 0.5f * pixelsPerStep;
        var prevY = geometry.ValueTop(Math.Clamp(note.Value, -TrackEditorGeometry.MaxValue,
                        TrackEditorGeometry.MaxValue)) +
                    rowHeight / 2;

        var index = 0;
        foreach (var (minutes, generated) in automation.ExpandNotes(note, 0, stepMinutes))
        {
            if (used >= cap) break;
            var x = noteX + (0.5f + (float)(minutes / stepMinutes)) * pixelsPerStep;
            var y = geometry.ValueTop(Math.Clamp(generated.Value, -TrackEditorGeometry.MaxValue,
                        TrackEditorGeometry.MaxValue)) +
                    rowHeight / 2;

            Line(ref used, prevX, prevY, x, y, color, maxY);
            Mark(ref used, x - 1f, y - rowHeight * 0.3f, 2f, rowHeight * 0.6f, color, maxY);
            if (x >= TrackEditorGeometry.GutterWidth && x <= _clipRight && y <= maxY)
                _handles.Add(new MarkerHandle(note, segment, index, x, y));

            index++;
            prevX = x;
            prevY = y;
        }

        // Where the note stops ringing - nothing is drawn when it is left to bleed on.
        if (automation is { CutAtEnd: true, End: > 0 } && used < cap)
        {
            var endX = noteX + automation.EndSteps(stepMinutes) * pixelsPerStep;
            var top = geometry.ValueTop(Math.Clamp(note.Value, -TrackEditorGeometry.MaxValue,
                TrackEditorGeometry.MaxValue));
            Mark(ref used, endX - 2f, top + 0.5f, 2f, Math.Max(1, rowHeight - 1), endColor, maxY);
        }
    }

    /// <summary>
    ///     One connecting line between two generated points, trimmed the same way a mark is.
    ///     Lines with both ends outside the viewport are dropped; one crossing it is drawn
    ///     whole, since a leaning line cannot be clipped by shrinking a rect.
    /// </summary>
    private void Line(ref int used, float x1, float y1, float x2, float y2, Vector4 color, float maxY)
    {
        if (Math.Max(x1, x2) < TrackEditorGeometry.GutterWidth || Math.Min(x1, x2) > _clipRight) return;
        if (Math.Min(y1, y2) > maxY) return;

        var slot = used++;
        var (left, right) = x1 <= x2 ? ((x1, y1), (x2, y2)) : ((x2, y2), (x1, y1));
        batch.SetLine(firstSlot + slot, _origin.X + left.Item1, _origin.Y + left.Item2,
            _origin.X + right.Item1, _origin.Y + right.Item2, 1f, color);

        var length = MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        var rect = new Vector4(left.Item1, left.Item2, length, 1f);
        if (slot < _marks.Count) _marks[slot] = rect;
        else _marks.Add(rect);
    }

    /// <summary>
    ///     Writes one mark, trimmed against the grid's bottom edge so no tick or cap bleeds
    ///     past a partially scrolled grid into the pinned cut row below it. Marks fully left
    ///     of the gutter or right of the viewport are dropped without taking a slot, so the
    ///     cost follows the viewport rather than the note's length.
    /// </summary>
    private void Mark(ref int used, float x, float y, float width, float height, Vector4 color, float maxY)
    {
        if (x + width < TrackEditorGeometry.GutterWidth || x > _clipRight) return;
        height = Math.Max(0, Math.Min(y + height, maxY) - y);

        var slot = used++;
        batch.Set(firstSlot + slot, _origin.X + x, _origin.Y + y, width, height, color);
        var rect = new Vector4(x, y, width, height);
        if (slot < _marks.Count) _marks[slot] = rect;
        else _marks.Add(rect);
    }
}

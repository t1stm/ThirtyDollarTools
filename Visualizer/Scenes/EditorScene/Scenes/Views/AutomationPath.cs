using OpenTK.Mathematics;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Views;

/// <summary>
///     Plots a note's generated automation events as a step path in the note's sound
///     color: a horizontal run at the current value, a vertical jump where a keyframe
///     changes it, and a short tick at every generated event, so a pure repeat still
///     reads as a line with marks on it. Time-mode gaps are mapped through the note's own
///     segment step rate, a display-only approximation once the path crosses into a
///     segment with another tempo. Draws into the tail slot range of
///     <see cref="TrackEditorView" />'s grid batch rather than owning elements - the path
///     takes no input. Being the batch's last range it grows, so a note may generate any
///     number of events; only off-screen marks are dropped.
/// </summary>
internal sealed class AutomationPath(LineBatch batch, int firstSlot)
{
    private readonly List<Vector4> _marks = [];
    private float _clipRight;
    private Vector2 _origin;

    /// <summary>Test seam: the rects the last layout drew, in view-local (x, y, width, height).</summary>
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
        float segStartPx, Vector4 color, ref int used)
    {
        var stepMinutes = segment.StepMinutes(track.Timing.BPM);
        if (stepMinutes <= 0) return;

        var maxY = geometry.GridBottom;
        _clipRight = geometry.ViewWidth;
        _origin = origin;
        var pixelsPerStep = geometry.PixelsPerStep;
        var scrollX = geometry.ScrollX;
        var rowHeight = geometry.RowHeight;
        var prevX = TrackEditorGeometry.GutterWidth + segStartPx + (note.Step + 0.5f) * pixelsPerStep - scrollX;
        var prevY = geometry.ValueTop(Math.Clamp(note.Value, -TrackEditorGeometry.MaxValue,
                        TrackEditorGeometry.MaxValue)) +
                    rowHeight / 2;

        foreach (var (minutes, generated) in note.Automation!.ExpandNotes(note, 0, stepMinutes))
        {
            var x = TrackEditorGeometry.GutterWidth + segStartPx +
                (note.Step + 0.5f + (float)(minutes / stepMinutes)) * pixelsPerStep - scrollX;
            var y = geometry.ValueTop(Math.Clamp(generated.Value, -TrackEditorGeometry.MaxValue,
                        TrackEditorGeometry.MaxValue)) +
                    rowHeight / 2;

            // The horizontal run, the value jump (only when the value moved), the tick.
            Mark(ref used, Math.Min(prevX, x), prevY - 0.5f, Math.Abs(x - prevX), 1f, color, maxY);
            if (Math.Abs(y - prevY) >= 1f)
                Mark(ref used, x - 0.5f, Math.Min(prevY, y), 1f, Math.Abs(y - prevY), color, maxY);
            Mark(ref used, x - 1f, y - rowHeight * 0.3f, 2f, rowHeight * 0.6f, color, maxY);

            prevX = x;
            prevY = y;
        }
    }

    /// <summary>
    ///     Writes one mark, trimmed against the grid's bottom edge so no run, jump or tick
    ///     bleeds past a partially scrolled grid into the pinned cut row below it. Marks fully
    ///     left of the gutter or right of the viewport are dropped without taking a slot, so
    ///     the cost follows the viewport rather than the track's length.
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

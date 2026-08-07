using OpenTK.Mathematics;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Views;

/// <summary>
///     Plots a note's generated automation events as a step path in the note's sound
///     color: a horizontal run at the current value, a vertical jump where a keyframe
///     changes it, and a short tick at every generated event (so pure repeats stay
///     visible as "a horizontal line with small vertical lines"). Time-mode gaps are
///     mapped through the note's own segment step rate - display-only approximation
///     when the path crosses into a segment with another tempo. Draws into the tail slot
///     range of <see cref="TrackEditorView" />'s grid batch rather than owning elements -
///     the path takes no input, so it needs no elements at all. That range is the batch's
///     last, so it grows with the paths instead of capping them: a note can generate any
///     number of events, and the old fixed pool simply stopped drawing once spent, cutting
///     long paths off mid-line. Only what is off-screen is dropped now. Split out of
///     <see cref="TrackEditorView" /> so automation display (curves, editing) can grow
///     independently.
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
    ///     tail. The slots that exist are exactly the ones drawn into, so the mark list
    ///     (which the same writes fill) is the high-water mark to release back to.
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
    ///     Trims a mark against the grid's bottom edge - the one shared point that
    ///     covers all three mark kinds (run, jump, tick) bleeding past a partially scrolled
    ///     grid into the pinned cut row below it. Marks fully left of the gutter or right of
    ///     the viewport are dropped without taking a slot: what is drawn is bounded by the
    ///     viewport, not by a pool, so a long track's off-screen paths cost nothing.
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

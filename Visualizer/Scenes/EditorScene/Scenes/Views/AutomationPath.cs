using OpenTK.Mathematics;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Views;

/// <summary>
///     Draws a note's generated automation over its body. Each keyframe instance repaints the
///     body from its own step on, filled at the volume it plays at, with a darker 1 px seam
///     on that step's grid line. Each instance also gets a marker drawn like a small note,
///     whose left edge is the step its retrigger starts on, on the row it plays at. A 1 px
///     path joins the note's own start to each marker's, so it shows where the value is going
///     instead of stepping horizontally and then jumping. A bright cap inside the body's
///     right end marks where a <see cref="AudioKeyframeManager.CutAtEnd" /> stops the note,
///     so one that ends reads apart from one that bleeds into what follows. Time-mode
///     positions are mapped through the note's own segment step rate, a display-only
///     approximation once the path crosses into a segment with another tempo.
///     Draws into a slot range of <see cref="TrackEditorView" />'s block batch rather than
///     owning elements - the path takes no input - sitting after the note pool so the marks
///     paint over the bodies. Marks outside the viewport are dropped without taking a slot,
///     so a 256-keyframe note costs what is on screen; past <paramref name="cap" /> the path
///     simply stops drawing.
///     Given the track's cuts it also plots the note's resumes - the instants another note's
///     automation silenced this one and auto-resume put it back - as hollow markers in the
///     end cap's color, drawn but not draggable.
/// </summary>
internal sealed class AutomationPath(LineBatch batch, int firstSlot, int cap, Func<Vector4, double?, Vector4> volumeFill)
{
    // The boards' numbers: the marker's corner, its height as a share of a row, its width as a
    // share of a step, how far its fill lifts off the band under it, the path's lift off the
    // instrument color, how much darker a seam is than its band, and the shadows' and the
    // outline's black.
    private const float MarkerRadius = 2f;
    private const float MarkerRowShare = 0.55f;
    private const float MarkerStepShare = 0.75f;
    private const float MarkerLift = 0.4f;
    private const float SelectedMarkerLift = 0.6f;
    private const float SeamSink = 0.35f;
    private const float ShadowAlpha = 0.6f;
    private const float OutlineAlpha = 0.5f;

    private readonly List<Mark> _marks = [];
    private readonly List<MarkerHandle> _handles = [];
    private readonly List<Point> _points = [];
    private float _clipRight;
    private Vector2 _origin;

    /// <summary>
    ///     Where the last layout put every visible keyframe marker, in view-local
    ///     coordinates - what the view hangs its draggable <see cref="KeyframeBlock" />
    ///     pool on. Rebuilt per layout, like the marks themselves.
    /// </summary>
    public IReadOnlyList<MarkerHandle> Handles => _handles;

    /// <summary>One visible marker: which keyframe it is, its centre, and its size.</summary>
    internal readonly record struct MarkerHandle(
        Note Note,
        TrackSegment Segment,
        int Index,
        float X,
        float Y,
        float Width,
        float Height);

    /// <summary>What a slot of the path holds - the test seam's way of telling them apart.</summary>
    internal enum MarkKind
    {
        Band,
        Seam,
        EndCap,
        Line,
        MarkerShadow,
        Marker,
        MarkerRing,
        MarkerOutline,
        Resume
    }

    /// <summary>
    ///     One written slot, in view-local coordinates: (x, y, width, height), or for a
    ///     <see cref="MarkKind.Line" /> (x, y, length, thickness) from its left-hand end, since a
    ///     leaning line has no axis-aligned rect to report.
    /// </summary>
    internal readonly record struct Mark(MarkKind Kind, Vector4 Rect, Vector4 Color);

    /// <summary>
    ///     The colors one note's path is drawn in: its own (the instrument's, lifted while
    ///     selected), the path's tint, the selection ring, and the end cap's.
    /// </summary>
    internal readonly record struct Look(Vector4 Color, bool Selected, Vector4 Path, Vector4 Ring, Vector4 EndCap);

    /// <summary>One instance the expansion generated, in steps from the note's start.</summary>
    private readonly record struct Point(float Steps, double Value, double? Volume, int Index);

    /// <summary>Clears the handle list; the view calls this once before the segment pass.</summary>
    public void BeginFrame()
    {
        _handles.Clear();
    }

    /// <summary>Test seam: every slot the last layout wrote.</summary>
    public IReadOnlyList<Mark> Marks => _marks;

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

    /// <param name="steps">The note's drawn length in steps - what the view sized its body to.</param>
    public void Draw(TrackEditorGeometry geometry, Vector2 origin, ProjectTrack track, TrackSegment segment, Note note,
        float segStartPx, float steps, Look look, float shading, ref int used, IReadOnlyList<CutPoint>? cuts = null)
    {
        var automation = note.Automation!;
        var stepMinutes = segment.StepMinutes(track.Timing.BPM);
        if (stepMinutes <= 0) return;

        _clipRight = geometry.ViewWidth;
        _origin = origin;
        var maxY = geometry.GridBottom;
        var pps = geometry.PixelsPerStep;
        var stepPx = segStartPx + note.Step * pps;

        // The cuts are on the track's own timeline, so the walk has to run there too and the
        // plot subtracts the note's start again. Without a cut list the note's own zero is
        // enough, and the extra segment walks are skipped.
        var noteMinutes = cuts is null ? 0 : track.MinutesAtStepPosition(track.GlobalStepOf(segment, note));
        _points.Clear();
        foreach (var (minutes, generated, index, _) in automation.ExpandNotes(note, noteMinutes, stepMinutes, cuts))
            _points.Add(new Point((float)((minutes - noteMinutes) / stepMinutes), Clamp(generated.Value),
                generated.Volume, index));

        // The body exactly as the view lays it out, so the bands and the cap land on it.
        var bodyX = geometry.CellX(stepPx);
        var bodyRight = bodyX + geometry.SpanWidth(steps);
        var bodyY = geometry.NoteTop(Clamp(note.Value));
        var bodyHeight = geometry.NoteHeight;
        var radius = TrackEditorView.CornerRadius(bodyRight - bodyX, bodyHeight);
        var lit = ShadedRect.Of(0, 0, strength: shading);

        // Bands: each keyframe repaints the body from its step's grid line on, at the volume
        // it plays at - the line itself is the seam, one shade darker. Only the last band
        // rounds, on the body's own right corners; the light is the body's, so the joins vanish.
        for (var i = 0; i < _points.Count; i++)
        {
            if (_points[i].Index < 0) continue;
            var seam = geometry.LineX(stepPx + _points[i].Steps * pps);
            if (seam >= bodyRight) continue;

            var next = bodyRight;
            for (var j = i + 1; j < _points.Count; j++)
            {
                if (_points[j].Index < 0) continue;
                next = Math.Min(next, geometry.LineX(stepPx + _points[j].Steps * pps));
                break;
            }

            // A band reaches back over the body (see ShadedRect.Shape), so its corners and its
            // light are measured on the body's own box rather than its slice of it.
            var fill = volumeFill(look.Color, _points[i].Volume);
            var start = Math.Max(seam + 1, bodyX);
            var corners = next >= bodyRight ? ShadedRect.TopRight | ShadedRect.BottomRight : 0;
            if (seam >= bodyX)
                Rect(MarkKind.Seam, ref used, seam, bodyY, 1, bodyHeight, Mix(fill, Vector4.UnitW, SeamSink * shading),
                    lit, maxY);
            Rect(MarkKind.Band, ref used, start, bodyY, next - start, bodyHeight, fill,
                ShadedRect.Of(radius, corners, -(start - bodyX), shading), maxY);
        }

        // Where the note stops ringing - nothing is drawn when it is left to bleed on. It is the
        // body's last 2 px, so it reaches back over the body to round on the body's corners.
        if (automation is { CutAtEnd: true, End: > 0 })
            Rect(MarkKind.EndCap, ref used, bodyRight - 2, bodyY, 2, bodyHeight, look.EndCap,
                ShadedRect.Of(radius, ShadedRect.TopRight | ShadedRect.BottomRight, -(bodyRight - 2 - bodyX)), maxY);

        // The path, under the markers: from the note's own start through every keyframe's,
        // each on the middle of the row it plays at. A resume stays off it: it plays the
        // value already running, and routing the lean through it would flatten the path the
        // keyframes describe.
        var prevX = bodyX;
        var prevY = geometry.RowMiddle(Clamp(note.Value));
        foreach (var point in _points)
        {
            if (point.Index < 0) continue;
            var x = geometry.CellX(stepPx + point.Steps * pps);
            var y = geometry.RowMiddle(point.Value);
            Line(ref used, prevX, prevY, x, y, look.Path, maxY);
            (prevX, prevY) = (x, y);
        }

        // The markers: small notes starting where their retrigger starts, everything drawn
        // inside the box, so its left edge - outline included - is the start.
        var gapSteps = automation.Gap <= 0 ? steps
            : automation.Timing == KeyframeTiming.Step ? automation.Gap : (float)(automation.Gap / 60d / stepMinutes);
        // Never wider than tall: zooming in past that keeps the marker square, not a bar.
        var height = Math.Max(3, MathF.Round(geometry.RowHeight * MarkerRowShare));
        var width = Math.Max(4, Math.Min(Math.Min(MathF.Round(MarkerStepShare * pps), height), gapSteps * pps - 2));
        var inset = MathF.Floor((geometry.RowHeight + 1 - height) / 2);
        var corner = TrackEditorView.CornerRadius(width, height, MarkerRadius, 11f);
        var shadow = new Vector4(0, 0, 0, ShadowAlpha * shading);
        var outline = new Vector4(0, 0, 0, OutlineAlpha);
        var box = ShadedRect.Of(corner);
        foreach (var point in _points)
        {
            var x = geometry.CellX(stepPx + point.Steps * pps);
            var y = geometry.ValueTop(point.Value) + inset;

            // A resume is somebody else's cut being repaired: the same box, hollow, in the end
            // cap's color, and no handle - it is automatic and carries nothing to drag.
            if (point.Index < 0)
            {
                Rect(MarkKind.Resume, ref used, x, y, width, height, look.EndCap,
                    ShadedRect.Of(corner, outline: 2), maxY);
                Rect(MarkKind.MarkerOutline, ref used, x, y, width, height, outline,
                    ShadedRect.Of(corner, outline: 1), maxY);
                continue;
            }

            if (pps >= TrackEditorView.MinShadowPixelsPerStep)
                Rect(MarkKind.MarkerShadow, ref used, x, y + 1, width, height, shadow, box, maxY);
            var fill = volumeFill(look.Color, point.Volume);
            Rect(MarkKind.Marker, ref used, x, y, width, height,
                Mix(fill, Vector4.One, look.Selected ? SelectedMarkerLift : MarkerLift),
                ShadedRect.Of(corner, strength: shading, light: ShadedRect.Light.Marker), maxY);
            if (look.Selected)
                Rect(MarkKind.MarkerRing, ref used, x, y, width, height, look.Ring,
                    ShadedRect.Of(corner, outline: 2), maxY);
            Rect(MarkKind.MarkerOutline, ref used, x, y, width, height, outline,
                ShadedRect.Of(corner, outline: 1), maxY);

            var (centreX, centreY) = (x + width / 2, y + height / 2);
            if (centreX >= TrackEditorGeometry.GutterWidth && centreX <= _clipRight && centreY <= maxY)
                _handles.Add(new MarkerHandle(note, segment, point.Index, centreX, centreY, width, height));
        }
    }

    private static double Clamp(double value)
    {
        return Math.Clamp(value, -TrackEditorGeometry.MaxValue, TrackEditorGeometry.MaxValue);
    }

    /// <summary>The same color mixed toward another, alpha untouched.</summary>
    private static Vector4 Mix(Vector4 color, Vector4 toward, float amount)
    {
        var mixed = Vector4.Lerp(color, toward, amount);
        mixed.W = color.W;
        return mixed;
    }

    /// <summary>
    ///     One connecting line between two generated points, trimmed the same way a mark is.
    ///     Lines with both ends outside the viewport are dropped; one crossing it is drawn
    ///     whole, since a leaning line cannot be clipped by shrinking a rect.
    /// </summary>
    private void Line(ref int used, float x1, float y1, float x2, float y2, Vector4 color, float maxY)
    {
        if (used >= cap) return;
        if (Math.Max(x1, x2) < TrackEditorGeometry.GutterWidth || Math.Min(x1, x2) > _clipRight) return;
        if (Math.Min(y1, y2) > maxY) return;

        var slot = used++;
        var (left, right) = x1 <= x2 ? ((x1, y1), (x2, y2)) : ((x2, y2), (x1, y1));
        batch.SetLine(firstSlot + slot, _origin.X + left.Item1, _origin.Y + left.Item2,
            _origin.X + right.Item1, _origin.Y + right.Item2, 1f, color);

        var length = MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        Record(slot, new Mark(MarkKind.Line, new Vector4(left.Item1, left.Item2, length, 1f), color));
    }

    /// <summary>
    ///     Writes one rect, trimmed against the grid's bottom edge so nothing bleeds past a
    ///     partially scrolled grid into the pinned cut row below it. Rects fully left of the
    ///     gutter or right of the viewport are dropped without taking a slot, so the cost
    ///     follows the viewport rather than the note's length.
    /// </summary>
    private void Rect(MarkKind kind, ref int used, float x, float y, float width, float height, Vector4 color,
        Vector4 shape, float maxY)
    {
        if (used >= cap || width <= 0) return;
        if (x + width < TrackEditorGeometry.GutterWidth || x > _clipRight) return;
        height = Math.Max(0, Math.Min(y + height, maxY) - y);

        var slot = used++;
        batch.Set(firstSlot + slot, _origin.X + x, _origin.Y + y, width, height, color, shape);
        Record(slot, new Mark(kind, new Vector4(x, y, width, height), color));
    }

    private void Record(int slot, Mark mark)
    {
        if (slot < _marks.Count) _marks[slot] = mark;
        else _marks.Add(mark);
    }
}

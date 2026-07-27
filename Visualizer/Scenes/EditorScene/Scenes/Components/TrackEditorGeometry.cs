using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Owns the piano roll's scroll/zoom state and every pixel↔model conversion for
///     <see cref="TrackEditorView" />. Plain state, no GL - the piece that makes the
///     roll's math unit-testable headless. Every coordinate in and out is local to the
///     view (relative to its own top-left), never absolute screen space.
/// </summary>
public sealed class TrackEditorGeometry
{
    public const int MaxValue = 60; // the TDW default value range
    public const int Rows = MaxValue * 2 + 1;
    public const float GutterWidth = 44f;
    public const float StripHeight = 22f;
    public const float RulerHeight = 18f;

    /// <summary>
    ///     Fixed-height pinned row for cut notes, always visible at the bottom of the
    ///     viewport - unlike every other row, it never scrolls, pans vertically, or zooms
    ///     with the grid.
    /// </summary>
    public const float CutRowHeight = 24f;

    /// <summary>1px rule separating the scrollable grid from the pinned cut row.</summary>
    public const float RuleHeight = 1f;

    public const float GridTop = StripHeight + RulerHeight;

    /// <summary>Top of the pinned cut row - docked to the bottom of the viewport, clamped
    /// so it never rises above <see cref="GridTop" /> in a viewport too short to fit it.</summary>
    public float CutRowTop { get; private set; }

    /// <summary>Bottom edge of the scrollable grid - the rule sits directly below it.</summary>
    public float GridBottom => CutRowTop - RuleHeight;

    /// <summary>Width of the viewport as of the last <see cref="SetViewport" /> - the right
    /// edge everything horizontal is culled against.</summary>
    public float ViewWidth { get; private set; }

    public readonly ViewNavigation Nav = new(minZoom: 4f, maxZoom: 128f) { Zoom = 64f };

    public float ScrollX { get => Nav.ScrollX; set => Nav.ScrollX = value; }
    public float ScrollY { get => Nav.ScrollY; set => Nav.ScrollY = value; }
    public float PixelsPerStep { get => Nav.Zoom; set => Nav.Zoom = value; }

    public const float MinRowHeight = 4f;
    public const float MaxRowHeight = 300f;

    /// <summary>Height of one value row, scaled by the user with <see cref="ScaleRows" />.</summary>
    public float RowHeight { get; set; } = 20f;

    /// <summary>A fresh view (and every OpenTrack) starts centered on value 0; stays
    /// pending until the user scrolls.</summary>
    public bool CenterPending { get; set; } = true;

    /// <summary>
    ///     Recomputes the vertical scroll bounds for the given viewport and, while
    ///     <see cref="CenterPending" />, centers scroll on value 0. Flex parents lay a
    ///     view out more than once per frame with different heights, so only the last
    ///     call before use counts.
    /// </summary>
    public void SetViewport(float width, float height)
    {
        ViewWidth = width;
        CutRowTop = Math.Max(GridTop, height - CutRowHeight);
        var gridHeight = Math.Max(0, GridBottom - GridTop);
        Nav.MaxScrollY = Math.Max(0, Rows * RowHeight - gridHeight);
        if (CenterPending) ScrollY = (Rows * RowHeight - gridHeight) / 2;
    }

    /// <summary>
    ///     Pointer-anchored vertical scaling (Ctrl+middle drag): the value under the
    ///     cursor stays put while the rows grow or shrink. <paramref name="localY" /> is
    ///     local to the view, <paramref name="dy" /> is the pointer's movement since the
    ///     last call - negative (upwards) grows the rows.
    /// </summary>
    public void ScaleRows(float localY, float dy)
    {
        var anchorRows = (localY - GridTop + ScrollY) / RowHeight;
        RowHeight = Math.Clamp(RowHeight * MathF.Pow(1.01f, -dy), MinRowHeight, MaxRowHeight);
        ScrollY = anchorRows * RowHeight - (localY - GridTop);
    }

    /// <summary>Maps a local x to (segment, local step); null outside the track's grid.</summary>
    public (TrackSegment? segment, int step) StepAt(ProjectTrack? track, float localX, bool clamp)
    {
        if (track == null) return (null, 0);

        var step = (int)Math.Floor((localX - GutterWidth + ScrollX) / PixelsPerStep);
        if (step < 0)
        {
            if (!clamp) return (null, 0);
            step = 0;
        }

        TrackSegment? last = null;
        var lastStep = 0;
        foreach (var segment in track.Segments)
        {
            if (segment.StepCount <= 0) continue;
            if (step < segment.StepCount) return (segment, step);
            step -= segment.StepCount;
            last = segment;
            lastStep = segment.StepCount - 1;
        }

        return clamp && last != null ? (last, lastStep) : (null, 0);
    }

    /// <summary>
    ///     Continuous, unsnapped step position - the same value for every segment since
    ///     they all share this view's pixels-per-step, so it doubles as the track-absolute
    ///     "global step" (see <see cref="ProjectTrack.GlobalStepOf" />) without walking
    ///     segments. Used by the marquee selection, which must not snap to the grid.
    /// </summary>
    public double UnsnappedStepAt(float localX)
    {
        return (localX - GutterWidth + ScrollX) / PixelsPerStep;
    }

    /// <summary>Continuous, unsnapped value position - the marquee's counterpart to <see cref="ValueAt" />.</summary>
    public double UnsnappedValueAt(float localY)
    {
        var r = (localY - GridTop + ScrollY) / RowHeight;
        return MaxValue - r;
    }

    public double ValueAt(float localY, bool fineSnap)
    {
        var r = (localY - GridTop + ScrollY) / RowHeight;
        var value = fineSnap
            ? Math.Round((MaxValue - (r - 0.5)) * 5) / 5
            : MaxValue - Math.Floor(r);
        return Math.Clamp(value, -MaxValue, MaxValue);
    }

    public float ValueTop(double value)
    {
        return GridTop + (float)((MaxValue - value) * RowHeight) - ScrollY;
    }

    public float SegmentStartPx(ProjectTrack track, TrackSegment target)
    {
        float offset = 0;
        foreach (var segment in track.Segments)
        {
            if (segment == target) break;
            offset += segment.StepCount * PixelsPerStep;
        }

        return offset;
    }

    /// <summary>
    ///     Horizontally, only the left edge is bounded (matching <see cref="ArrangementView" />'s
    ///     unbounded-right pan) - the grid can scroll past the last segment into empty space,
    ///     so playhead-follow can keep centering the playhead all the way to the end of
    ///     playback instead of freezing once the content itself fills the viewport.
    /// </summary>
    public void ClampScroll() => Nav.Clamp();

    /// <summary>Pointer-anchored zoom (Ctrl+wheel): the step under the cursor stays put.
    /// <paramref name="pointerPx" /> is local x already offset past the gutter.</summary>
    public void ZoomAt(float pointerPx, float wheelDeltaY) => Nav.ZoomAt(pointerPx, wheelDeltaY);
}

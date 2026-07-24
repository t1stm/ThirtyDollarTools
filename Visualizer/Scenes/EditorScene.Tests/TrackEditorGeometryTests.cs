using EditorScene.Scenes.Components;

namespace EditorScene.Tests;

public class TrackEditorGeometryTests
{
    private static TrackEditorGeometry MakeGeometry(float pixelsPerStep = 16f)
    {
        var geometry = new TrackEditorGeometry { PixelsPerStep = pixelsPerStep };
        // Rows=121; height chosen so (GridBottom - GridTop) / Rows == 20 exactly (i.e.
        // gridHeight == Rows * 20, past the pinned cut row and its rule), and the
        // initial CenterPending centers ScrollY to 0 for clean row math below.
        geometry.SetViewport(800,
            TrackEditorGeometry.GridTop + TrackEditorGeometry.Rows * 20 +
            TrackEditorGeometry.CutRowHeight + TrackEditorGeometry.RuleHeight, 20);
        return geometry;
    }

    [Fact]
    public void StepAt_MapsLocalXToSegmentAndStep()
    {
        var geometry = MakeGeometry();
        var state = new EditorState();
        var track = state.AddTrack(); // default segment: 16 steps

        var localX = TrackEditorGeometry.GutterWidth + 5 * geometry.PixelsPerStep + 8; // mid-cell 5
        var (segment, step) = geometry.StepAt(track, localX, false);

        Assert.Same(track.Segments[0], segment);
        Assert.Equal(5, step);
    }

    [Fact]
    public void StepAt_BeforeGutter_ReturnsNullUnlessClamped()
    {
        var geometry = MakeGeometry();
        var state = new EditorState();
        var track = state.AddTrack();

        var (segment, _) = geometry.StepAt(track, 0, false);
        Assert.Null(segment);

        var (clamped, step) = geometry.StepAt(track, 0, true);
        Assert.Same(track.Segments[0], clamped);
        Assert.Equal(0, step);
    }

    [Fact]
    public void ValueAt_MapsLocalYToRowValue()
    {
        var geometry = MakeGeometry();

        Assert.Equal(TrackEditorGeometry.MaxValue, geometry.ValueAt(TrackEditorGeometry.GridTop + 5, false));
        Assert.Equal(TrackEditorGeometry.MaxValue - 3, geometry.ValueAt(TrackEditorGeometry.GridTop + 20 * 3 + 5, false));
    }

    [Fact]
    public void ValueTop_IsTheInverseOfValueAt()
    {
        var geometry = MakeGeometry();

        var top = geometry.ValueTop(TrackEditorGeometry.MaxValue - 3);
        Assert.Equal(TrackEditorGeometry.MaxValue - 3, geometry.ValueAt(top + 1, false));
    }

    [Fact]
    public void ZoomAt_KeepsTheStepUnderThePointerFixed()
    {
        var geometry = MakeGeometry(64f);
        geometry.ScrollX = 50f;
        const float pointerPx = 100f;
        var anchorStepsBefore = (pointerPx + geometry.ScrollX) / geometry.PixelsPerStep;

        geometry.ZoomAt(pointerPx, 1f);

        var anchorStepsAfter = (pointerPx + geometry.ScrollX) / geometry.PixelsPerStep;
        Assert.True(geometry.PixelsPerStep > 64f);
        Assert.Equal(anchorStepsBefore, anchorStepsAfter, 3);
    }

    [Fact]
    public void ZoomAt_ClampsToTheZoomRange()
    {
        var geometry = MakeGeometry(64f);

        geometry.ZoomAt(0f, 100f);
        Assert.Equal(128f, geometry.PixelsPerStep);

        geometry.ZoomAt(0f, -100f);
        Assert.Equal(4f, geometry.PixelsPerStep);
    }

    [Fact]
    public void ClampScroll_AllowsScrollingPastTheContentsRightEdge()
    {
        var geometry = MakeGeometry();

        // Only the left edge is bounded - scrolling can push the grid's content
        // fully past the viewport (matching ArrangementView's unbounded-right pan),
        // so playhead-follow can keep centering the playhead near the end of a
        // track instead of freezing once the last segment fills the viewport.
        geometry.ScrollX = 1000f;
        geometry.ClampScroll();

        Assert.Equal(1000f, geometry.ScrollX);
    }

    [Fact]
    public void ClampScroll_StillClampsTheLeftEdge()
    {
        var geometry = MakeGeometry();

        geometry.ScrollX = -50f;
        geometry.ClampScroll();

        Assert.Equal(0f, geometry.ScrollX);
    }

    [Fact]
    public void UnsnappedStepAt_IsContinuous_UnlikeStepAt()
    {
        var geometry = MakeGeometry(); // 16 px/step

        // Mid-cell 5, a quarter of the way into the next step: StepAt floors to 5,
        // the marquee's unsnapped version keeps the fraction.
        var localX = TrackEditorGeometry.GutterWidth + 5 * geometry.PixelsPerStep + 4;
        Assert.Equal(5.25, geometry.UnsnappedStepAt(localX), 3);
    }

    [Fact]
    public void UnsnappedValueAt_IsTheContinuousInverseOfValueTop()
    {
        var geometry = MakeGeometry();

        var top = geometry.ValueTop(12.5);
        Assert.Equal(12.5, geometry.UnsnappedValueAt(top), 3);
    }

    [Fact]
    public void SetViewport_DocksTheCutRowAtTheBottom_AboveA1pxRule()
    {
        var geometry = new TrackEditorGeometry { PixelsPerStep = 16f };

        geometry.SetViewport(800, 414, 8);

        Assert.Equal(414 - TrackEditorGeometry.CutRowHeight, geometry.CutRowTop);
        Assert.Equal(geometry.CutRowTop - TrackEditorGeometry.RuleHeight, geometry.GridBottom);

        // gridHeight = 349, Rows = 121: 349/121 < 8, so RowHeight clamps to the minimum
        // and the grid scrolls instead of stretching.
        Assert.Equal(8f, geometry.RowHeight);
        Assert.Equal(TrackEditorGeometry.Rows * 8f - (geometry.GridBottom - TrackEditorGeometry.GridTop),
            geometry.Nav.MaxScrollY);
    }

    [Fact]
    public void SetViewport_ClampsTheCutRowAtGridTop_InATinyViewport()
    {
        var geometry = new TrackEditorGeometry { PixelsPerStep = 16f };

        // Shorter than GridTop + CutRowHeight: the cut row would have to rise above
        // the ruler, so it clamps to GridTop instead and the grid collapses to 0 height -
        // RowHeight's own Math.Max(minRowHeight, ...) guards the division, so it still
        // reports the minimum rather than dividing by (or producing) a negative height.
        geometry.SetViewport(800, 50, 8);

        Assert.Equal(TrackEditorGeometry.GridTop, geometry.CutRowTop);
        Assert.True(geometry.GridBottom <= TrackEditorGeometry.GridTop); // no usable grid height left
        Assert.Equal(8f, geometry.RowHeight); // clamped to the minimum, not a negative/NaN division
    }
}

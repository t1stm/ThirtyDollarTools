using EditorScene.Scenes.Components;

namespace EditorScene.Tests;

public class TrackEditorGeometryTests
{
    private static TrackEditorGeometry MakeGeometry(float pixelsPerStep = 16f)
    {
        var geometry = new TrackEditorGeometry { PixelsPerStep = pixelsPerStep };
        // Rows=121; height chosen so (height - GridTop) / Rows == 20 exactly, and the
        // initial CenterPending centers ScrollY to 0 for clean row math below.
        geometry.SetViewport(800, TrackEditorGeometry.GridTop + TrackEditorGeometry.Rows * 20, 20);
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

        // Only the left edge is bounded — scrolling can push the grid's content
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
}

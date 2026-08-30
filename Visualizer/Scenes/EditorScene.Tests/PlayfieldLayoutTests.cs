using VisualizerScene.Objects.Playfield;

namespace EditorScene.Tests;

/// <summary>
///     The bound a chunk's <c>EndY</c> is derived from. A block that never wraps - anything
///     under one line's worth, which is most faithful sequences - leaves
///     <see cref="LayoutHandler.Height" /> at 0, so a chunk bottom must come from Y instead:
///     taken from Height it lands above the scroller's clip and
///     <c>EventCanvas.RefreshCulling</c> drops every chunk.
/// </summary>
public class PlayfieldLayoutTests
{
    [Fact]
    public void PartialLine_LeavesHeightAtZero_SoBoundsMustComeFromY()
    {
        var layout = new LayoutHandler(64, 16, new GapBox(6), new GapBox(0, 400, 0, 0));
        layout.Reset();

        for (var i = 0; i < 6; i++) layout.GetNewPosition(false);

        // Six of sixteen: no line break, so Height never moved off Reset's 0 while Y is
        // still the block's own top.
        Assert.Equal(0, layout.Height);
        Assert.Equal(400, layout.Y);
        Assert.True(layout.Y + layout.Size > layout.Y, "a chunk's bottom must sit below its top");
    }

    [Fact]
    public void FullLine_MovesHeightToY_SoBothAgreeOnceItWraps()
    {
        var layout = new LayoutHandler(64, 16, new GapBox(6), new GapBox(0, 400, 0, 0));
        layout.Reset();

        for (var i = 0; i < 16; i++) layout.GetNewPosition(false);

        Assert.Equal(layout.Y, layout.Height);
    }
}

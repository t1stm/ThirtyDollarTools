using OpenTK.Mathematics;
using ThirtyDollarConverter.Parser;
using VisualizerScene.Objects.Playfield;
using VisualizerScene.Objects.Playfield.Batch;

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

    /// <summary>
    ///     The visualizer builds only the chunks on screen, so it culls by
    ///     <see cref="ChunkGenerator.LayOut" />'s bounds and follows playback into unbuilt chunks
    ///     by <see cref="ChunkGenerator.BoxPosition" /> - both must land every event where one
    ///     walk of the whole sequence does, dividers and chunk seams included.
    /// </summary>
    [Fact]
    public void LayOutAndBoxPosition_MatchOneWalkOfTheWholeSequence()
    {
        var events = new BaseEvent[1300];
        for (var i = 0; i < events.Length; i++)
            events[i] = new NormalEvent { SoundEvent = i % 37 == 5 || i == 511 ? "!divider" : "a" };

        LayoutHandler NewLayout() => new(64, 16, new GapBox(6), new GapBox(0, 400, 0, 0));
        var generator = new ChunkGenerator(null!, NewLayout());
        var layouts = generator.LayOut(events);

        var walk = NewLayout();
        walk.Reset();
        var expected = new Vector2[events.Length];
        for (var i = 0; i < events.Length; i++)
            expected[i] = walk.GetNewPosition(events[i].SoundEvent == "!divider");

        Assert.Equal(3, layouts.Length);
        Assert.Equal(walk.Height, generator.LayoutHandler.Height);
        for (var chunk = 0; chunk < layouts.Length; chunk++)
        {
            var first = chunk * generator.ChunkSize;
            var last = Math.Min(events.Length, first + generator.ChunkSize) - 1;
            Assert.Equal(expected[first].Y, layouts[chunk].StartY);
            Assert.True(layouts[chunk].EndY > expected[last].Y, $"chunk {chunk} ends above its last event");
        }

        var lookup = new ChunkGenerator(null!, NewLayout());
        for (var i = 0; i < events.Length; i++)
            Assert.Equal(expected[i], lookup.BoxPosition(events, i, layouts[i / lookup.ChunkSize].State));
    }
}

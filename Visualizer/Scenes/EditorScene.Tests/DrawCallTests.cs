using EditorScene.Scenes.Components;
using Sundex.Components.Abstractions;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Text;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Views;
using Shared.Renderer.Planes;

namespace EditorScene.Tests;

/// <summary>
///     Counts what one frame of the two canvas views actually costs in draw calls
///     (see <see cref="EditorTestContext.DrawCalls" />) - the check behind the
///     batching in TrackEditorView/ArrangementView. A regression here means something
///     went back to one plane (or one text buffer) per visible item.
/// </summary>
public class DrawCallTests(ITestOutputHelper output)
{
    /// <summary>A dense chart: 8 segments of 4 bars, 4 notes on every step.</summary>
    private static (EditorTestContext ctx, TrackEditorView view) DenseTrack()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        var instrument = state.AddInstrument("boom");
        state.SetInstrumentSounds(instrument, [new InstrumentSound { Sound = "boom" }]);

        for (var i = 0; i < 8; i++)
        {
            var segment = i == 0 ? track.Segments[0] : state.AddSegment(track);
            segment.Bars = 4;
            for (var step = 0; step < segment.StepCount; step++)
            for (var value = 0; value < 4; value++)
                state.AddNote(segment, step, instrument, value);
        }

        state.OpenTrack(track);
        var view = EditorTestContext.Styled(new TrackEditorView(ctx, state)
            { Width = 1600, Height = 800, PixelsPerStep = 16f });
        view.Layout();
        view.DrawTo(ctx);
        return (ctx, view);
    }

    [Fact]
    public void DenseNoteEditor_CostsAHandfulOfDrawCalls()
    {
        var (ctx, _) = DenseTrack();
        var calls = Report(ctx);

        // The line batch, the label batch, and the few pieces still painted by a plane of
        // their own (gutter, cut rule, playhead/marquee when shown). Every note, strip,
        // automation mark and caption is inside one of the two batches.
        Assert.True(calls.Count <= 8, $"{calls.Count} draw calls for one dense note editor frame");
        Assert.Empty(calls.OfType<TextBuffer>());
    }

    [Fact]
    public void Arrangement_CostsAHandfulOfDrawCalls_AtAnyClipCount()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var view = EditorTestContext.Styled(new ArrangementView(ctx, state) { Width = 1600, Height = 800 });
        // 200 clips - past the batch's 64-slot reservation, so this also covers the grow
        // path: every clip must still be painted, none dropped at the reservation's edge.
        for (var i = 0; i < 8; i++)
        {
            var track = state.AddTrack();
            for (var j = 0; j < 25; j++) state.PlaceTrack(track, i, j * 4);
        }

        view.Refresh();
        view.Layout();
        view.DrawTo(ctx);
        var calls = Report(ctx);

        // Lines and every clip fill in one batch; the bar numbers and the 200 track names
        // in one batch each. Nothing scales with the clip count.
        Assert.True(calls.Count <= 8, $"{calls.Count} draw calls for 200 clips");
        Assert.Single(calls.OfType<LineBatch>());
        Assert.Equal(2, calls.OfType<LabelBatch>().Count());
        Assert.Empty(calls.OfType<TextBuffer>());
        Assert.All(view.Blocks, block => Assert.Equal(view.ClipColor, view.FillOf(block)));
    }

    [Fact]
    public void AnAutomationPath_KeepsDrawing_PastTheBatchReservation()
    {
        // Regression: the marks came from a fixed 768-slot pool, and a path that ran past
        // it simply stopped being drawn - a note's automation line ended mid-air.
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var track = state.AddTrack();
        var instrument = state.AddInstrument("boom");
        state.SetInstrumentSounds(instrument, [new InstrumentSound { Sound = "boom" }]);

        var segment = track.Segments[0];
        segment.Bars = 64; // wide enough that the whole path stays on screen at 4 px/step
        // ~3 marks per generated event, 500 events: far past the 768-slot reservation.
        state.AddNote(segment, 0, instrument, 0).Automation = new AudioKeyframeManager
        {
            Repeats = 500,
            Keyframes = { new AudioKeyframe { Gap = 1, Value = new Modifier(1) } }
        };

        state.OpenTrack(track);
        var view = EditorTestContext.Styled(new TrackEditorView(ctx, state)
            { Width = 1600, Height = 800, PixelsPerStep = 4f });
        view.Layout();
        view.DrawTo(ctx);

        Assert.True(view.AutomationMarks.Count > 768,
            $"path truncated at {view.AutomationMarks.Count} marks");
        // And the grown path is still two draw calls: the grid batch and the block batch.
        Assert.Equal(2, Report(ctx).OfType<LineBatch>().Count());
    }

    private List<IRenderable> Report(EditorTestContext ctx)
    {
        var calls = ctx.DrawCalls();
        output.WriteLine($"total draw calls: {calls.Count}");
        foreach (var group in calls.GroupBy(c => c.GetType().Name).OrderByDescending(g => g.Count()))
            output.WriteLine($"  {group.Key}: {group.Count()}");
        output.WriteLine($"  (planes: {calls.Count(c => c is ColoredPlane)}, text: {calls.Count(c => c is TextBuffer)})");
        return calls;
    }
}

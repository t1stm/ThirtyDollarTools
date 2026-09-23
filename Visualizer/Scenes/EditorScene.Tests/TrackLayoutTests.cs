using EditorScene.State;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

/// <summary>
///     <see cref="TrackLayout" /> is a lookup standing in for <see cref="ProjectTrack" />'s own
///     walks over the segments, so it has to answer exactly what they answer - zero-length
///     placeholder segments and steps off either end included.
/// </summary>
public class TrackLayoutTests
{
    [Fact]
    public void AnswersLikeTheTracksOwnWalks()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        track.Segments[0].Bars = 2;
        track.NewSegment().Bars = 0; // a placeholder: no steps at all
        track.NewSegment().StepsPerBeat = 3;
        track.NewSegment().Bars = 0;
        track.NewSegment().Numerator = 7;

        var note = new Note { Step = 5, Instrument = Instrument.Single("boom") };
        track.Segments[2].Notes.Add(note);

        var layout = new TrackLayout(track);
        Assert.Equal(track.Segments.Sum(segment => segment.StepCount), layout.TotalSteps);

        for (var step = -2; step <= layout.TotalSteps + 2; step++)
            Assert.Equal(track.SegmentAtGlobalStep(step), layout.SegmentAt(step));

        foreach (var segment in track.Segments)
            Assert.Equal(track.GlobalStepOf(segment, 3), layout.GlobalStepOf(segment, 3));
        Assert.Equal(3, layout.GlobalStepOf(new TrackSegment(), 3)); // not in the track

        Assert.Same(track.Segments[2], layout.SegmentOf(note));
        Assert.Null(layout.SegmentOf(new Note { Step = 5, Instrument = note.Instrument }));
    }
}

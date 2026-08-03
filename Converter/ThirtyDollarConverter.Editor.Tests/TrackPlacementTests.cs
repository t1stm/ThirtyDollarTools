using ThirtyDollarConverter.Objects;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     The FL-style arrangement layer: a ProjectTrack is a reusable pattern; a
///     TrackPlacement puts it on a channel at a position on the project timeline
///     (in quarter notes at the root BPM). Only placed patterns sound.
/// </summary>
public class TrackPlacementTests
{
    private static ProjectTrack OneBarQuarterGrid(ThirtyDollarProject project)
    {
        // 4/4, one bar, quarter-note steps at 120 BPM: 4 steps of 0.5 s each.
        var track = project.NewTrack();
        track.Segments[0].StepsPerBeat = 1;
        return track;
    }

    [Fact]
    public void PatternPlacedTwice_RepeatsBackToBack()
    {
        var project = new ThirtyDollarProject();
        var track = OneBarQuarterGrid(project);
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        track.Segments[0].Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("snare") });

        project.Place(track, 0, 0);
        project.Place(track, 1, 4); // next bar; the channel is visual only

        var events = project.ToSequence().Events;

        Assert.Equal(["!speed", "!divider", "kick", "!stop", "snare", "!stop", "kick", "!stop", "snare"],
            events.Select(e => e.SoundEvent));
        Assert.Equal(120, events[0].Value); // quarter grid at 120 BPM
        Assert.All(events.Where(e => e.SoundEvent == "!stop"), e => Assert.Equal(1, e.Value));
    }

    [Fact]
    public void GapBetweenPlacements_KeepsExactAbsoluteTiming()
    {
        var project = new ThirtyDollarProject();
        var track = OneBarQuarterGrid(project);
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });

        project.Place(track, 0, 0);
        project.Place(track, 0, 8); // one empty bar between the clips

        var sequence = project.ToSequence();

        // The silent bar must survive as time: kicks at steps 0 and 8.
        Assert.Equal(["!speed", "!divider", "kick", "!stop", "kick"],
            sequence.Events.Select(e => e.SoundEvent));
        Assert.Equal(7, sequence.Events[3].Value);

        const uint sample_rate = 48000;
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = sample_rate });
        var placements = calculator.CalculateOne(sequence).Where(p => p.Audible).ToArray();
        Assert.Equal(2, placements.Length);
        // 8 quarter notes at 120 BPM = 4 s.
        Assert.Equal(4d * sample_rate, placements[1].Index - (double)placements[0].Index);
    }

    [Fact]
    public void UnplacedPattern_IsSilent()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var events = project.ToSequence().Events;

        Assert.DoesNotContain(events, e => !e.SoundEvent!.StartsWith('!'));
    }

    [Fact]
    public void RemoveTrack_RemovesItsPlacements()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var other = project.NewTrack();
        project.Place(track, 0, 0);
        project.Place(track, 1, 4);
        var kept = project.Place(other, 2, 0);

        Assert.True(project.RemoveTrack(track));

        Assert.Equal([kept], project.Placements);
    }

    [Fact]
    public void RemovePlacement_RemovesOnlyThatClip()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var first = project.Place(track, 0, 0);
        var second = project.Place(track, 0, 4);

        Assert.True(project.RemovePlacement(first));
        Assert.False(project.RemovePlacement(first));

        Assert.Equal([second], project.Placements);
        Assert.Equal([track], project.Tracks); // the pattern itself stays
    }

    [Fact]
    public void Place_RejectsATrackFromAnotherProject()
    {
        var project = new ThirtyDollarProject();
        var foreign = new ThirtyDollarProject().NewTrack();

        Assert.Throws<ArgumentException>(() => project.Place(foreign, 0, 0));
    }

    [Fact]
    public void PatternDuration_IsExposedForTheArrangementView()
    {
        var project = new ThirtyDollarProject();
        var track = OneBarQuarterGrid(project); // one 4/4 bar at 120 BPM
        track.NewSegment().StepsPerBeat = 1; // second identical bar

        Assert.Equal(2d * 2 / 60, track.DurationMinutes(), 9); // 2 bars x 2 s
    }
}
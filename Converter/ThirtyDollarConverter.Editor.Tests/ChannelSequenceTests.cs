using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     Per-channel sequences for editor playback: each arrangement lane renders on its
///     own audio channel, so every lane's sequence must share the merged sequence's
///     timeline origin — otherwise the mixed lanes drift apart.
/// </summary>
public class ChannelSequenceTests
{
    private const uint SampleRate = 48000;

    private static ProjectTrack OneBarQuarterGrid(ThirtyDollarProject project)
    {
        // 4/4, one bar, quarter-note steps at 120 BPM: 4 steps of 0.5 s each.
        var track = project.NewTrack();
        track.Segments[0].StepsPerBeat = 1;
        return track;
    }

    private static double[] AudibleSeconds(Sequence sequence)
    {
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = SampleRate });
        return calculator.CalculateOne(sequence)
            .Where(p => p.Audible)
            .Select(p => p.Index / (double)SampleRate)
            .ToArray();
    }

    [Fact]
    public void SingleChannelProject_MatchesTheMergedSequence()
    {
        var project = new ThirtyDollarProject();
        var track = OneBarQuarterGrid(project);
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        project.Place(track, 0, 0);
        project.Place(track, 0, 4);

        Assert.Equal(SequenceText.Serialize(project.ToSequence()),
            SequenceText.Serialize(project.ChannelSequence(0)));
    }

    [Fact]
    public void Channels_ShareTheProjectTimelineOrigin()
    {
        var project = new ThirtyDollarProject();
        var kick = OneBarQuarterGrid(project);
        kick.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        var snare = OneBarQuarterGrid(project);
        snare.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("snare") });

        project.Place(kick, 0, 0);
        project.Place(snare, 1, 4); // second bar, other lane

        var merged_times = AudibleSeconds(project.ToSequence());
        // The snare lane alone must keep the snare where the merged timeline has it.
        Assert.Equal(merged_times[1], AudibleSeconds(project.ChannelSequence(1)).Single(), 9);
        Assert.Equal(merged_times[0], AudibleSeconds(project.ChannelSequence(0)).Single(), 9);
    }

    [Fact]
    public void FirstClipOffsetFromZero_KeepsAbsoluteTime()
    {
        // A project whose first clip sits at bar 2 must render it at its absolute
        // time: rendered time = arrangement time + the TDW lead-in (every sequence
        // starts one step at the initial 300 BPM before its first "!speed" = 0.2 s).
        // The editor playhead relies on this fixed mapping.
        var project = new ThirtyDollarProject();
        var track = OneBarQuarterGrid(project);
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        project.Place(track, 0, 4); // 4 quarters at 120 BPM = 2 s in

        Assert.Equal(2.2, AudibleSeconds(project.ToSequence()).Single(), 9);
        Assert.Equal(2.2, AudibleSeconds(project.ChannelSequence(0)).Single(), 9);
    }

    [Fact]
    public void EmptyChannel_HasNoAudibleEvents()
    {
        var project = new ThirtyDollarProject();
        var track = OneBarQuarterGrid(project);
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        project.Place(track, 0, 0);

        Assert.Empty(AudibleSeconds(project.ChannelSequence(3)));
    }
}

using ThirtyDollarParser;

namespace ThirtyDollarConverter.Editor.Tests;

public class SequenceTextTests
{
    [Fact]
    public void BuiltSequence_RoundTripsThroughFromString_WithFullPrecision()
    {
        // 120 + 121 BPM share no grid within the multiplier bound, so the export
        // carries exact fractional stops — the values Stringify's 2-decimal
        // rounding would corrupt.
        var project = new ThirtyDollarProject();
        var a = project.NewTrack();
        a.Segments[0].Notes.Add(new Note { Step = 0, Sound = "kick" });
        a.Segments[0].Notes.Add(new Note { Step = 7, Sound = "snare", Value = 1.5 });

        var b = project.NewTrack();
        b.Timing = new TimingInfo { BPM = 121 };
        b.Segments[0].Notes.Add(new Note { Step = 5, Sound = "hat" });

        project.Place(a, 0, 0);
        project.Place(b, 1, 0);

        var sequence = project.ToSequence();
        Assert.Contains(sequence.Events, e => e.SoundEvent == "!stop" && e.Value != Math.Round(e.Value, 2));

        var parsed = Sequence.FromString(SequenceText.Serialize(sequence));

        Assert.Equal(sequence.Events.Select(e => e.SoundEvent), parsed.Events.Select(e => e.SoundEvent));
        Assert.All(sequence.Events.Zip(parsed.Events),
            pair => Assert.Equal(pair.First.Value, pair.Second.Value, 1e-6));
        Assert.Equal(sequence.UsedSounds, parsed.UsedSounds);
    }
}

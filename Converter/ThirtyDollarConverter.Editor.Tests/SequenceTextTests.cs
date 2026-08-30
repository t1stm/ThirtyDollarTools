using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor.Tests;

public class SequenceTextTests
{
    [Fact]
    public void IndividualCut_StringifiesPerSound_NotAsABareGlobalCut()
    {
        var single = new Sequence { Events = [new IndividualCutEvent(["a"])] };
        Assert.Equal("!cut@a", SequenceText.Serialize(single));

        var multi = new Sequence { Events = [new IndividualCutEvent(["a", "b"])] };
        var text = SequenceText.Serialize(multi);
        Assert.True(text is "!cut@a|!cut@b" or "!cut@b|!cut@a", text);
    }

    /// <summary>
    ///     An event built without stating a ValueScale serializes without one, so an exported
    ///     gap is a bare "_pause" and never "_pause@/".
    /// </summary>
    [Fact]
    public void ExportedPauses_CarryNoValueScale()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        track.Segments[0].Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("kick") });
        project.Place(track, 0, 0);

        // The default style, which is what the export dialog starts on: a short gap renders
        // as "_pause"s rather than "!stop".
        var text = SequenceText.Serialize(project.ToSequence(new SequenceStyle()));

        Assert.Contains("_pause", text);
        Assert.DoesNotContain("_pause@", text);
    }

    [Fact]
    public void BuiltSequence_RoundTripsThroughFromString_WithFullPrecision()
    {
        // 120 + 121 BPM share no grid within the multiplier bound, so the export
        // carries exact fractional stops - the values Stringify's 2-decimal
        // rounding would corrupt.
        var project = new ThirtyDollarProject();
        var a = project.NewTrack();
        a.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        a.Segments[0].Notes.Add(new Note { Step = 7, Instrument = Instrument.Single("snare"), Value = 1.5 });

        var b = project.NewTrack();
        b.Timing = new TimingInfo { BPM = 121 };
        b.Segments[0].Notes.Add(new Note { Step = 5, Instrument = Instrument.Single("hat") });

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
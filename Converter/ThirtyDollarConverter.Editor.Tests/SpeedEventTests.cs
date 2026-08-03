using ThirtyDollarParser;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     How a tempo region turns into "!speed" text: the BPM and the grid resolution stay
///     separate factors, so a reader sees "120 BPM, four steps a beat" instead of "480".
/// </summary>
public class SpeedEventTests
{
    private static ProjectTrack Track(params (int StepsPerBeat, float? Bpm)[] segments)
    {
        var track = new ProjectTrack(new TimingInfo { BPM = 120 }, 1);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = i == 0 ? track.Segments[0] : track.NewSegment();
            segment.Bars = 1;
            segment.StepsPerBeat = segments[i].StepsPerBeat;
            segment.BPM = segments[i].Bpm;
            segment.Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        }

        return track;
    }

    private static string[] Speeds(ProjectTrack track)
    {
        return track.ToSequence().Events
            .Where(e => e.SoundEvent == "!speed")
            .Select(e => e.Stringify())
            .ToArray();
    }

    [Fact]
    public void FirstRegion_SplitsIntoBpmThenStepMultiplier()
    {
        Assert.Equal(["!speed@120", "!speed@4@x"], Speeds(Track((4, null))));
    }

    [Fact]
    public void OneStepPerBeat_NeedsNoMultiplier()
    {
        Assert.Equal(["!speed@120"], Speeds(Track((1, null))));
    }

    [Fact]
    public void ResolutionChangeAtTheSameBpm_EmitsOnlyTheRatio()
    {
        // 4 -> 8 steps a beat doubles the grid; 8 -> 4 halves it back.
        Assert.Equal(["!speed@120", "!speed@4@x", "!speed@2@x", "!speed@2@/"],
            Speeds(Track((4, null), (8, null), (4, null))));
    }

    [Fact]
    public void BpmChange_RestatesTheBpmAndTheMultiplier()
    {
        Assert.Equal(["!speed@120", "!speed@4@x", "!speed@140", "!speed@4@x"],
            Speeds(Track((4, null), (4, 140f))));
    }

    [Fact]
    public void RatioThatIsNotAWholeFactor_RestatesTheBpmInstead()
    {
        // 4 -> 6 steps a beat is 1.5x: expressing it as a ratio would need a value the
        // serialized precision can't hold exactly, so the pair is restated in full.
        Assert.Equal(["!speed@120", "!speed@4@x", "!speed@120", "!speed@6@x"],
            Speeds(Track((4, null), (6, null))));
    }

    [Fact]
    public void ExportedText_KeepsTheMultiplierSuffix()
    {
        var text = SequenceText.Serialize(Track((4, null), (8, null)).ToSequence());

        Assert.StartsWith("!speed@120|\n!speed@4@x|\n!divider|\nboom", text);
        Assert.Contains("!speed@2@x", text);
    }

    [Fact]
    public void TheOpeningTempo_IsClosedOffWithADivider()
    {
        // Even with no divider styling asked for: the header gets its own line.
        var events = Track((1, null)).ToSequence().Events;

        Assert.Equal(["!speed", "!divider", "boom"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void TheSplitSpeedRoundTripsThroughTheParser()
    {
        var track = Track((4, null), (8, null), (4, null), (4, 140f));
        var text = SequenceText.Serialize(track.ToSequence());

        // The grid rate the parsed text actually asks for, region by region.
        var bpm = 0d;
        var rates = new List<double>();
        foreach (var ev in Sequence.FromString(text).Events)
        {
            if (ev.SoundEvent != "!speed")
            {
                if (ev.SoundEvent == "boom") rates.Add(bpm);
                continue;
            }

            bpm = ev.ValueScale switch
            {
                ValueScale.Times => bpm * ev.Value,
                ValueScale.Divide => bpm / ev.Value,
                _ => ev.Value
            };
        }

        Assert.Equal([480, 960, 480, 560], rates);
    }
}
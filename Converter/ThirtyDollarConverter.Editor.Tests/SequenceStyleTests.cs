using ThirtyDollarConverter.Objects;

namespace ThirtyDollarConverter.Editor.Tests;

public class SequenceStyleTests
{
    private static ProjectTrack MakeTrack(float bpm = 120)
    {
        return new ProjectTrack(new TimingInfo { BPM = bpm }, 1);
    }

    [Fact]
    public void DividerEveryFourBars_SplitsAtTheBarLine_WithoutMovingAnything()
    {
        var track = MakeTrack(); // quarters at 120/min, 4 steps per bar
        track.Segments[0].StepsPerBeat = 1;
        track.Segments[0].Bars = 8;
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 16, Instrument = Instrument.Single("kick") }); // downbeat of bar 5

        var sequence = track.ToSequence(new SequenceStyle { DividerEveryBars = 4 });

        // The silence belongs to the old bar; the divider opens the new one.
        Assert.Equal(["!speed", "boom", "!stop", "!divider", "kick"],
            sequence.Events.Select(e => e.SoundEvent));

        // Dividers take no time: the kick still lands 16 half-second steps in.
        const uint sample_rate = 48000;
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = sample_rate });
        var placements = calculator.CalculateOne(sequence).Where(p => p.Audible).ToArray();
        Assert.Equal(placements[0].Index + 16 * 24000ul, placements[1].Index);
    }

    [Fact]
    public void DividerOnSpeedChanges_MarksEveryTempoRegionButNotTheFirst()
    {
        var track = MakeTrack();
        var slow = track.Segments[0]; // 4 quarters at 60 BPM
        slow.BPM = 60;
        slow.StepsPerBeat = 1;

        var next = track.NewSegment(); // inherited 120 BPM quarters
        next.StepsPerBeat = 1;
        next.Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var events = track.ToSequence(
            new SequenceStyle { DividerOnSpeedChanges = true, MigrateToStop = 1 }).Events;

        Assert.Equal(["!speed", "!stop", "!divider", "!speed", "boom"],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void BarLineMeetingASpeedChange_EmitsOnlyOneDivider()
    {
        var track = MakeTrack();
        track.Segments[0].StepsPerBeat = 1;
        track.Segments[0].Bars = 4; // bar divider lands exactly on the segment change

        var fine = track.NewSegment(); // sixteenth grid, its own region
        fine.Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var style = new SequenceStyle { DividerEveryBars = 4, DividerOnSpeedChanges = true };
        var events = track.ToSequence(style).Events;

        Assert.Equal(["!speed", "!stop", "!divider", "!speed", "boom"],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void SpeedChange_ResetsTheBarCounter()
    {
        var track = MakeTrack();
        track.Segments[0].StepsPerBeat = 1; // quarters, 4 steps per bar
        track.Segments[0].Bars = 2; // two leftover bars that must NOT carry over
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var fine = track.NewSegment(); // sixteenth grid, 16 steps per bar
        fine.Bars = 6;
        fine.Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        fine.Notes.Add(new Note { Step = 32, Instrument = Instrument.Single("snare") }); // bar 2 after the change
        fine.Notes.Add(new Note { Step = 64, Instrument = Instrument.Single("hat") }); // bar 4 after the change

        var style = new SequenceStyle
        { DividerEveryBars = 4, DividerOnSpeedChanges = true, MigrateToStop = 1 };
        var events = track.ToSequence(style).Events;

        // Without the reset, the two leftover bars would push the bar divider onto the
        // snare (2 + 2 = 4). The tempo change starts a fresh count, so it lands on the
        // hat, four bars into the new section.
        Assert.Equal(
            [
                "!speed", "boom", "!stop", "!divider", "!speed", "kick",
                "!stop", "snare", "!stop", "!divider", "hat"
            ],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void MigrateToStopNull_RendersEveryWholeGapAsPauses()
    {
        var track = MakeTrack();
        track.Segments[0].StepsPerBeat = 1;
        track.Segments[0].Notes.Add(new Note { Step = 3, Instrument = Instrument.Single("boom") });

        var sequence = track.ToSequence(new SequenceStyle { MigrateToStop = null });

        Assert.Equal(["!speed", "_pause", "_pause", "_pause", "boom"],
            sequence.Events.Select(e => e.SoundEvent));

        // "_pause" == "!stop@1": the boom still lands three half-second steps in.
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = 48000 });
        var placements = calculator.CalculateOne(sequence).ToArray();
        var boom = placements.Single(p => p.Audible);
        Assert.Equal(placements[0].Index + 3 * 24000ul, boom.Index);
    }

    [Fact]
    public void MigrateToStopThreshold_SplitsShortGapsFromLongOnes()
    {
        var track = MakeTrack();
        track.Segments[0].StepsPerBeat = 1;
        track.Segments[0].Bars = 4;
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 3, Instrument = Instrument.Single("kick") }); // gap 2, below 4
        track.Segments[0].Notes.Add(new Note { Step = 9, Instrument = Instrument.Single("snare") }); // gap 5, at/above 4

        var events = track.ToSequence(new SequenceStyle { MigrateToStop = 4 }).Events;

        Assert.Equal(["!speed", "boom", "_pause", "_pause", "kick", "!stop", "snare"],
            events.Select(e => e.SoundEvent));
        Assert.Equal(5, events[5].Value);
    }

    [Fact]
    public void FractionalGaps_AlwaysStayStops()
    {
        // A 7 ms slapback sits off any grid; even in pause-only mode its gap must
        // remain a fractional "!stop" - "_pause"s can only count whole steps.
        var track = MakeTrack();
        var slapback = new AudioKeyframeManager { Timing = KeyframeTiming.Time };
        slapback.Keyframes.Add(new AudioKeyframe { Gap = 0.007f });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Automation = slapback });

        var events = track.ToSequence(new SequenceStyle { MigrateToStop = null }).Events;

        var stop = Assert.Single(events, e => e.SoundEvent == "!stop");
        Assert.NotEqual(Math.Round(stop.Value), stop.Value);
    }

    [Fact]
    public void SilenceSwallowingSeveralBarLines_YieldsASingleDivider()
    {
        var track = MakeTrack(); // 4 steps per bar
        track.Segments[0].StepsPerBeat = 1;
        track.Segments[0].Bars = 8;
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 30, Instrument = Instrument.Single("kick") }); // mid-bar 8

        var events = track.ToSequence(new SequenceStyle { DividerEveryBars = 2 }).Events;

        // Bars 2, 4 and 6 all pass inside one stretch of silence: one divider, not three.
        Assert.Equal(["!speed", "boom", "!stop", "!divider", "kick"],
            events.Select(e => e.SoundEvent));
    }
}
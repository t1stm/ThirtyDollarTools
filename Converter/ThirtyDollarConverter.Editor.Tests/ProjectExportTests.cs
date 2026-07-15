using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Objects;

namespace ThirtyDollarConverter.Editor.Tests;

public class ProjectExportTests
{
    [Fact]
    public void EmptyProject_ExportsOnlySpeed()
    {
        var project = new ThirtyDollarProject();
        project.NewTrack();

        var events = project.ToSequence().Events;

        var ev = Assert.Single(events);
        Assert.Equal("!speed", ev.SoundEvent);
    }

    [Fact]
    public void TwoTracks_MergeInStepOrder()
    {
        var project = new ThirtyDollarProject();
        var drums = project.NewTrack();
        var melody = project.NewTrack();

        drums.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom" });
        drums.Segments[0].Notes.Add(new Note { Step = 2, Sound = "clap" });
        melody.Segments[0].Notes.Add(new Note { Step = 1, Sound = "noteblock_harp" });

        var events = project.ToSequence().Events;

        Assert.Equal(["!speed", "boom", "noteblock_harp", "clap"],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void NotesOnSameStepAcrossTracks_AreCombined()
    {
        var project = new ThirtyDollarProject();
        var a = project.NewTrack();
        var b = project.NewTrack();

        a.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom" });
        b.Segments[0].Notes.Add(new Note { Step = 0, Sound = "clap" });

        var events = project.ToSequence().Events;

        Assert.Equal(["!speed", "boom", "!combine", "clap"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void TracksWithDifferentResolutions_AlignOnTheFinestGrid()
    {
        var project = new ThirtyDollarProject();
        var coarse = project.NewTrack();
        var fine = project.NewTrack();
        coarse.Segments[0].StepsPerBeat = 2;
        fine.Segments[0].StepsPerBeat = 4;

        // Both notes sit half a beat in: coarse step 1 of 2, fine step 2 of 4.
        coarse.Segments[0].Notes.Add(new Note { Step = 1, Sound = "boom" });
        fine.Segments[0].Notes.Add(new Note { Step = 2, Sound = "clap" });

        var events = project.ToSequence().Events;

        // Merged grid runs at the finest track grid: 4 steps per beat at 120 BPM.
        Assert.Equal(480, events[0].Value);
        Assert.Equal(["!speed", "!stop", "boom", "!combine", "clap"],
            events.Select(e => e.SoundEvent));
        Assert.Equal(2, events[1].Value);
    }

    [Fact]
    public void TracksWithDifferentTempos_MergeOnAnExactCommonGrid()
    {
        var project = new ThirtyDollarProject();
        var a = project.NewTrack();
        var b = project.NewTrack();
        a.Timing = new TimingInfo { BPM = 120 };
        b.Timing = new TimingInfo { BPM = 90 };
        a.Segments[0].StepsPerBeat = 1;
        b.Segments[0].StepsPerBeat = 1;

        a.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom" });
        a.Segments[0].Notes.Add(new Note { Step = 1, Sound = "kick" }); // 0.5 s in
        b.Segments[0].Notes.Add(new Note { Step = 1, Sound = "snare" }); // 2/3 s in

        var events = project.ToSequence().Events;

        // Smallest speed where both tempos land on whole steps: lcm(120, 90) = 360.
        Assert.Equal(360, events[0].Value);
        Assert.Equal(["!speed", "boom", "!stop", "kick", "snare"],
            events.Select(e => e.SoundEvent));
        Assert.Equal(2, events[2].Value); // kick sits on step 3, one step after boom
    }

    [Fact]
    public void IncommensurateTempos_KeepExactTimingWithFractionalStops()
    {
        var project = new ThirtyDollarProject();
        var a = project.NewTrack();
        var b = project.NewTrack();
        a.Timing = new TimingInfo { BPM = 120 };
        b.Timing = new TimingInfo { BPM = 121 };

        a.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom" });
        a.Segments[0].Notes.Add(new Note { Step = 4, Sound = "kick" }); // beat 1 at 120 BPM, 0.5 s
        b.Segments[0].Notes.Add(new Note { Step = 4, Sound = "snare" }); // beat 1 at 121 BPM, ~0.4959 s

        var sequence = project.ToSequence();
        var events = sequence.Events;

        // An exact common grid needs !speed@58080 = 120x the fastest rate, past the
        // 64x merge bound - these tempos count as "slightly differing". The fastest
        // grid (484) wins and the off-grid kick rides a "!combine|!stop@0.033"
        // cancel instead of being snapped.
        Assert.Equal(484, events[0].Value);
        Assert.Equal(["!speed", "boom", "!stop", "snare", "!combine", "!stop", "kick"],
            events.Select(e => e.SoundEvent));
        Assert.Equal(3, events[2].Value);
        Assert.Equal(484d / 120 - 4, events[5].Value, 1e-4);

        // Timing must be exact, not approximated.
        const uint sample_rate = 48000;
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = sample_rate });
        var placements = calculator.CalculateOne(sequence).Where(p => p.Audible).ToArray();

        var start = (double)placements[0].Index;
        var true_samples = new Dictionary<string, double>
        {
            ["boom"] = 0,
            ["kick"] = 4d / (120 * 4) * 60 * sample_rate,
            ["snare"] = 4d / (121 * 4) * 60 * sample_rate
        };

        Assert.Equal(3, placements.Length);
        foreach (var placement in placements)
            Assert.InRange(placement.Index - start - true_samples[placement.Event.SoundEvent!],
                -16, 16); // only the calculator's per-chunk sample truncation remains
    }

    [Fact]
    public void ExtremeBpm_ExportsUncappedOnItsOwnGrid()
    {
        // BMS gimmick territory: a 9,990,400 BPM track exports at its real grid rate.
        // There is no speed cap, so the gaps stay whole steps instead of microscopic
        // fractional stops.
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        track.Timing = new TimingInfo { BPM = 9_990_400 };
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom" });
        track.Segments[0].Notes.Add(new Note { Step = 4, Sound = "clap" });

        var events = project.ToSequence().Events;

        Assert.Equal(["!speed", "boom", "!stop", "clap"], events.Select(e => e.SoundEvent));
        Assert.Equal(4d * 9_990_400, events[0].Value); // sixteenth grid of the 4/4 default
        Assert.Equal(3, events[2].Value);
    }

    [Fact]
    public void Export_ListsUsedSounds()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom" });

        Assert.Contains("boom", project.ToSequence().UsedSounds);
    }
}

using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Editor.Tests;

public class ProjectExportTests
{
    [Fact]
    public void EmptyProject_ExportsOnlySpeed()
    {
        var project = new ThirtyDollarProject();
        project.Place(project.NewTrack(), 0, 0);

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

        drums.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        drums.Segments[0].Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("clap") });
        melody.Segments[0].Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("noteblock_harp") });
        project.Place(drums, 0, 0);
        project.Place(melody, 1, 0);

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

        a.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        b.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("clap") });
        project.Place(a, 0, 0);
        project.Place(b, 1, 0);

        var events = project.ToSequence().Events;

        Assert.Equal(["!speed", "boom", "!combine", "clap"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void NoteOnATwoSoundInstrument_LayersBothSounds()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var layer = project.NewInstrument("Layer");
        layer.AddSound("boom");
        layer.AddSound("clap");
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;

        Assert.Equal(["!speed", "boom", "!combine", "clap"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void TwoNotesOfDifferentInstruments_OnTheSameStep_CombineAcrossAllTheirSounds()
    {
        var project = new ThirtyDollarProject();
        var a = project.NewTrack();
        var b = project.NewTrack();

        var layerA = project.NewInstrument("A");
        layerA.AddSound("boom");
        layerA.AddSound("kick");
        var layerB = project.NewInstrument("B");
        layerB.AddSound("clap");
        layerB.AddSound("snare");

        a.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layerA });
        b.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layerB });
        project.Place(a, 0, 0);
        project.Place(b, 1, 0);

        var events = project.ToSequence().Events;

        Assert.Equal(["!speed", "boom", "!combine", "kick", "!combine", "clap", "!combine", "snare"],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void AutomationOnAnInstrumentNote_GeneratedEventsCarryEveryInstrumentSound()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var layer = project.NewInstrument("Layer");
        layer.AddSound("boom");
        layer.AddSound("clap");

        var echo = new AudioKeyframeManager();
        echo.Keyframes.Add(new AudioKeyframe { Gap = 2 });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer, Automation = echo });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;

        Assert.Equal(
            ["!speed", "boom", "!combine", "clap", "!stop", "boom", "!combine", "clap"],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void EmptyInstrument_YieldsNoEvents()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var empty = project.NewInstrument("Empty");
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = empty });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;

        var ev = Assert.Single(events);
        Assert.Equal("!speed", ev.SoundEvent);
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
        coarse.Segments[0].Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("boom") });
        fine.Segments[0].Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("clap") });
        project.Place(coarse, 0, 0);
        project.Place(fine, 1, 0);

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

        a.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        a.Segments[0].Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("kick") }); // 0.5 s in
        b.Segments[0].Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("snare") }); // 2/3 s in
        project.Place(a, 0, 0);
        project.Place(b, 1, 0);

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

        a.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        a.Segments[0].Notes.Add(new Note { Step = 4, Instrument = Instrument.Single("kick") }); // beat 1 at 120 BPM, 0.5 s
        b.Segments[0].Notes.Add(new Note { Step = 4, Instrument = Instrument.Single("snare") }); // beat 1 at 121 BPM, ~0.4959 s
        project.Place(a, 0, 0);
        project.Place(b, 1, 0);

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
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 4, Instrument = Instrument.Single("clap") });
        project.Place(track, 0, 0);

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
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        project.Place(track, 0, 0);

        Assert.Contains("boom", project.ToSequence().UsedSounds);
    }

    [Fact]
    public void TrackWithNullTranspose_InheritsTheProjectWideTranspose()
    {
        var project = new ThirtyDollarProject { Transpose = 2 };
        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Value = 5 });
        project.Place(track, 0, 0);

        var ev = project.ToSequence().Events[1];

        Assert.Equal(7, ev.Value);
    }

    [Fact]
    public void TrackWithOwnTranspose_OverridesTheProjectWideTranspose_EvenWhenZero()
    {
        var project = new ThirtyDollarProject { Transpose = 2 };
        var track = project.NewTrack();
        track.Transpose = 0; // explicit "no shift", not "unset"
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Value = 5 });
        project.Place(track, 0, 0);

        var ev = project.ToSequence().Events[1];

        Assert.Equal(5, ev.Value);
    }

    [Fact]
    public void CutNote_ExportsIndividualCut_IgnoringValueVolumePanOffset()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note
        {
            Step = 0,
            Instrument = Instrument.Single("kick"),
            IsCut = true,
            Value = 61,
            Volume = 50,
            Pan = 30,
            Offset = 0.5
        });
        project.Place(track, 0, 0);

        var sequence = project.ToSequence();
        var ev = Assert.IsType<IndividualCutEvent>(sequence.Events[1]);

        Assert.Equal("!cut", ev.SoundEvent);
        Assert.Equal(["kick"], ev.CutSounds);
        Assert.Equal(0, ev.Value);

        // PCMEncoder.GenerateAudioAndMixer pre-allocates a mixer track per separated
        // channel before rendering, so a cut's target track is guaranteed to exist -
        // the text parser (TryIndividualCutTDW) populates this as a parsing side effect;
        // building the event list programmatically must populate it identically, or a
        // cut silently no-ops against a track that was never created (the sequence would
        // parse and even look correct, but render byte-identical to having no cut at all).
        Assert.Contains("kick", sequence.SeparatedChannels);
    }

    [Fact]
    public void CutNote_LayeredInstrument_CutsEveryOneOfItsSounds()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var layer = project.NewInstrument("Layer");
        layer.AddSound("kick");
        layer.AddSound("clap");
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = layer, IsCut = true });
        project.Place(track, 0, 0);

        var ev = Assert.IsType<IndividualCutEvent>(project.ToSequence().Events[1]);

        Assert.Equal(["clap", "kick"], ev.CutSounds.OrderBy(s => s));
    }

    [Fact]
    public void CutNote_TransposeDoesNotLeakIntoTheExportedValue()
    {
        var project = new ThirtyDollarProject { Transpose = 5 };
        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick"), IsCut = true });
        project.Place(track, 0, 0);

        var ev = project.ToSequence().Events[1];

        Assert.Equal(0, ev.Value);
    }

    [Fact]
    public void CutNote_Alone_DoesNotShiftFollowingNotes()
    {
        // Regression for the SequenceBuilder latent bug: an action (here "!cut@sound", same
        // as an automation-generated cut) never advances the clock in playback, unlike a
        // sound. A cut group must not silently consume the step after it - one "!stop@1"
        // must appear before the following on-grid note.
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("kick"), IsCut = true });
        track.Segments[0].Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("clap") });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;

        Assert.Equal(["!speed", "boom", "!cut", "!stop", "clap"], events.Select(e => e.SoundEvent));
        Assert.Equal(1, events[3].Value);
    }

    [Fact]
    public void CutNote_SharingAStepWithASound_IsEmittedFirstAndConsumesOneStep()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick"), IsCut = true });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("clap") });
        project.Place(track, 0, 0);

        var events = project.ToSequence().Events;

        Assert.Equal(["!speed", "!cut", "boom", "clap"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void CutsOnTheSameStep_CollapseIntoOne_AcrossTracksAndNotes()
    {
        var project = new ThirtyDollarProject();
        var shared = project.NewInstrument("Shared");
        shared.AddSound("kick");

        var a = project.NewTrack();
        a.Segments[0].Notes.Add(new Note { Step = 0, Instrument = shared, IsCut = true });
        a.Segments[0].Notes.Add(new Note
            { Step = 0, Instrument = Instrument.Single("clap"), IsCut = true }); // different sound, same step
        var b = project.NewTrack();
        b.Segments[0].Notes.Add(new Note { Step = 0, Instrument = shared, IsCut = true }); // duplicate of a's
        project.Place(a, 0, 0);
        project.Place(b, 1, 0);

        var events = project.ToSequence().Events;
        var cut = Assert.IsType<IndividualCutEvent>(Assert.Single(events, e => e is IndividualCutEvent));

        Assert.Equal(["clap", "kick"], cut.CutSounds.OrderBy(s => s));
        // the shared instrument's own set must survive the merge untouched
        Assert.Equal(["kick"], shared.Sounds.Select(sound => sound.Sound));
    }

    [Fact]
    public void DividersKeepComing_PastTheFirstPlacementsTrack()
    {
        var project = new ThirtyDollarProject { RootTiming = { BPM = 120 } };

        // One short clip placed at the very start (4 bars of 4/4), then a long one after it.
        var stab = project.NewTrack();
        stab.Segments[0].StepsPerBeat = 1;
        stab.Segments[0].Bars = 4;
        stab.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var loop = project.NewTrack();
        loop.Segments[0].StepsPerBeat = 1;
        loop.Segments[0].Bars = 12;
        for (var bar = 0; bar < 12; bar++)
            loop.Segments[0].Notes.Add(new Note { Step = bar * 4, Instrument = Instrument.Single("kick") });

        project.Place(stab, 0, 0); // first in the list: used to be the only source of bar lines
        project.Place(loop, 1, 16);

        var events = project.ToSequence(new SequenceStyle { DividerEveryBars = 2 }).Events;

        // Bars 2 and 4 both pass in the silence before the loop and collapse to one divider,
        // then bars 6..16 give one each: 6. Following the first placement's track alone the
        // bar lines ran out after bar 4 and the whole loop got none.
        Assert.Equal(6, events.Count(e => e.SoundEvent == "!divider"));
    }
}
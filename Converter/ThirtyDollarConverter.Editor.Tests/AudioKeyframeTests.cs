using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Editor.Tests;

public class AudioKeyframeTests
{
    private static ProjectTrack MakeTrack()
    {
        return new ProjectTrack(new TimingInfo { BPM = 120 }, 1);
    }

    [Fact]
    public void StepKeyframes_GenerateADecayingEcho()
    {
        var track = MakeTrack(); // 4/4, sixteenth grid at 480 steps/min
        var echo = new AudioKeyframeManager();
        echo.Keyframes.Add(new AudioKeyframe { Gap = 1, Volume = new Modifier(0.5, ModifierKind.Multiply) });
        echo.Keyframes.Add(new AudioKeyframe { Gap = 1, Volume = new Modifier(0.5, ModifierKind.Multiply) });

        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Automation = echo });

        var events = track.ToSequence().Events;

        // One note becomes three, one grid step apart, each keyframe halving the last.
        Assert.Equal(["!speed", "boom", "boom", "boom"], events.Select(e => e.SoundEvent));
        Assert.Equal(480, events[0].Value);
        Assert.Null(events[1].Volume);
        Assert.Equal(50, events[2].Volume);
        Assert.Equal(25, events[3].Volume);
    }

    [Fact]
    public void TimeKeyframes_PlaceExactlyBySeconds()
    {
        var track = MakeTrack();
        var slapback = new AudioKeyframeManager { Timing = KeyframeTiming.Time };
        slapback.Keyframes.Add(new AudioKeyframe { Gap = 0.007f }); // 7 ms, off any musical grid

        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Automation = slapback });

        const uint sample_rate = 48000;
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = sample_rate });
        var placements = calculator.CalculateOne(track.ToSequence())
            .Where(p => p.Audible)
            .ToArray();

        // 7 ms = 336 samples; the fractional-stop fallback must keep it exact.
        Assert.Equal(2, placements.Length);
        Assert.InRange((double)placements[1].Index - placements[0].Index, 335, 337);
    }

    [Fact]
    public void TimeKeyframes_StayExactAcrossASpeedChange()
    {
        // An echo generated 0.6 s after the last quarter of a 120 BPM bar lands 0.1 s
        // into the following 240 BPM region - off that grid, so it must ride a
        // fractional stop and stay sample-exact across the "!speed" change.
        var track = MakeTrack();
        track.Segments[0].StepsPerBeat = 1; // 4 quarters of 0.5 s
        var echo = new AudioKeyframeManager { Timing = KeyframeTiming.Time };
        echo.Keyframes.Add(new AudioKeyframe { Gap = 0.6f });
        track.Segments[0].Notes.Add(new Note { Step = 3, Instrument = Instrument.Single("boom"), Automation = echo });

        var fast = track.NewSegment();
        fast.BPM = 240;
        fast.StepsPerBeat = 1;

        const uint sample_rate = 48000;
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = sample_rate });
        var placements = calculator.CalculateOne(track.ToSequence())
            .Where(p => p.Audible)
            .ToArray();

        // 0.6 s = 28800 samples after the base note.
        Assert.Equal(2, placements.Length);
        Assert.InRange((double)placements[1].Index - placements[0].Index, 28799, 28801);
    }

    [Fact]
    public void Modifiers_ClampPanAndFloorVolume()
    {
        var track = MakeTrack();
        var automation = new AudioKeyframeManager();
        automation.Keyframes.Add(new AudioKeyframe
        {
            Gap = 1,
            Pan = new Modifier(80),
            Volume = new Modifier(-200)
        });
        automation.Keyframes.Add(new AudioKeyframe { Gap = 1, Pan = new Modifier(80) });

        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Automation = automation });

        var events = track.ToSequence().Events;

        var first = Assert.IsType<ExtendedEvent>(events[2]);
        Assert.Equal(80, first.Pan);
        Assert.Equal(0, first.Volume); // 100 - 200, floored at silence

        var second = Assert.IsType<ExtendedEvent>(events[3]);
        Assert.Equal(100, second.Pan); // 80 + 80, clamped to full right
        Assert.Equal(0, second.Volume);
    }

    [Fact]
    public void Repeats_RunTheKeyframeList_CompoundingEachPass()
    {
        var track = MakeTrack();
        var echo = new AudioKeyframeManager { Repeats = 3 };
        echo.Keyframes.Add(new AudioKeyframe { Gap = 1, Volume = new Modifier(0.5, ModifierKind.Multiply) });

        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Automation = echo });

        var events = track.ToSequence().Events;

        // One keyframe repeated 3 times = 3 echoes, each halving the previous pass.
        Assert.Equal(["!speed", "boom", "boom", "boom", "boom"], events.Select(e => e.SoundEvent));
        Assert.Equal([50d, 25d, 12.5], events.Skip(2).Select(e => e.Volume));
    }

    [Fact]
    public void OffsetKeyframes_WalkTheSoundStart_AndSurviveTheTextExport()
    {
        var track = MakeTrack();
        // The note starts 0.25 s into the sound; each repeat jumps one beat later on
        // the grid AND another 0.5 s deeper into the sound (Kris's scrub use case).
        var scrub = new AudioKeyframeManager { Repeats = 2 };
        scrub.Keyframes.Add(new AudioKeyframe { Gap = 4, Offset = new Modifier(0.5) });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Offset = 0.25, Automation = scrub });

        var events = track.ToSequence().Events;
        Assert.Equal([0.25, 0.75, 1.25],
            events.OfType<ExtendedEvent>().Select(e => e.OffsetInSeconds));

        // The exact serializer keeps volume/pan/offset (BaseEvent-only text dropped them).
        var parsed = Sequence.FromString(SequenceText.Serialize(track.ToSequence()));
        Assert.Equal([0.25, 0.75, 1.25],
            parsed.Events.OfType<ExtendedEvent>().Select(e => e.OffsetInSeconds));
        Assert.Equal([100d, 100d],
            parsed.Events.OfType<ExtendedEvent>().Skip(1).Select(e => e.Volume!.Value));
    }

    [Fact]
    public void Repeats_SurviveTheProjectFileRoundTrip_AndOldFilesDefaultToOne()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var instrument = project.NewInstrument("boom");
        instrument.Sounds.Add("boom");
        var echo = new AudioKeyframeManager { Repeats = 4 };
        echo.Keyframes.Add(new AudioKeyframe { Gap = 1, Offset = new Modifier(0.5) });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = instrument, Offset = 0.25, Automation = echo });

        var loadedNote = ProjectFile.Load(ProjectFile.Save(project)).Tracks[0].Segments[0].Notes[0];
        Assert.Equal(4, loadedNote.Automation!.Repeats);
        Assert.Equal(0.25, loadedNote.Offset);
        Assert.Equal(new Modifier(0.5), loadedNote.Automation.Keyframes[0].Offset);

        // Repeats of 1 is never written, so the file is identical to a pre-feature one —
        // and a missing key loads back as 1.
        echo.Repeats = 1;
        var legacy = ProjectFile.Save(project);
        Assert.DoesNotContain("repeats", legacy);
        Assert.Equal(1, ProjectFile.Load(legacy).Tracks[0].Segments[0].Notes[0].Automation!.Repeats);
    }

    [Fact]
    public void CutKeyframe_CutsThenPlacesTheNoteAfterTheCut()
    {
        var track = MakeTrack(); // 4/4 sixteenth grid at 480 steps/min
        var cut = new AudioKeyframeManager();
        cut.Keyframes.Add(new AudioKeyframe { Gap = 4, Cut = true });

        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("loop"), Automation = cut });

        var events = track.ToSequence().Events;
        var cut_event = Assert.Single(events.OfType<IndividualCutEvent>());
        Assert.Equal(["loop"], cut_event.CutSounds);

        // The cut lands before the retriggered note in the same group.
        var cut_index = Array.IndexOf(events, (BaseEvent)cut_event);
        var retrigger_index = Array.IndexOf(events, events.Skip(cut_index + 1).First(e => e.SoundEvent == "loop"));
        Assert.True(cut_index < retrigger_index);

        const uint sample_rate = 48000;
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = sample_rate });
        var placements = calculator.CalculateOne(track.ToSequence())
            .Where(p => p.Audible)
            .ToArray();

        // The base note, the cut, and the retriggered note — cut and retrigger share a
        // sample index (4 steps at 480 steps/min = 0.5 s = 24000 samples after the note).
        Assert.Equal(3, placements.Length);
        Assert.IsType<IndividualCutEvent>(placements[1].Event);
        Assert.Equal("loop", placements[2].Event.SoundEvent);
        Assert.InRange((double)placements[1].Index - placements[0].Index, 23999, 24001);
        Assert.Equal(placements[1].Index, placements[2].Index);
    }

    [Fact]
    public void Keyframe_OldFileWithNoCutKey_LoadsAsNotCut()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var instrument = project.NewInstrument("boom");
        instrument.Sounds.Add("boom");
        var automation = new AudioKeyframeManager();
        automation.Keyframes.Add(new AudioKeyframe { Gap = 1 });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = instrument, Automation = automation });

        var saved = ProjectFile.Save(project);
        Assert.DoesNotContain("cut", saved); // never-cut keyframe writes no "cut" key at all

        var loaded = ProjectFile.Load(saved).Tracks[0].Segments[0].Notes[0];
        Assert.False(loaded.Automation!.Keyframes[0].Cut);
    }

    [Fact]
    public void SharedManager_ExpandsEachNoteIndependently()
    {
        // The segment-level use case: one manager instance across many notes.
        var track = MakeTrack();
        var octave_up = new AudioKeyframeManager();
        octave_up.Keyframes.Add(new AudioKeyframe { Gap = 1, Value = new Modifier(12) });

        var notes = track.Segments[0].Notes;
        notes.Add(new Note { Step = 0, Instrument = Instrument.Single("harp"), Value = 0, Automation = octave_up });
        notes.Add(new Note { Step = 8, Instrument = Instrument.Single("harp"), Value = 7, Automation = octave_up });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "harp", "harp", "!stop", "harp", "harp"],
            events.Select(e => e.SoundEvent));
        // Each expansion starts from its own base note, not from shared state.
        Assert.Equal([0, 12, 7, 19],
            events.Where(e => e.SoundEvent == "harp").Select(e => e.Value));
    }
}
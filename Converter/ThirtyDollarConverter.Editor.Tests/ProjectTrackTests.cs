using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Editor.Tests;

public class ProjectTrackTests
{
    private static ProjectTrack MakeTrack(float bpm = 120, int stepsPerBeat = 4)
    {
        var track = new ProjectTrack(new TimingInfo { BPM = bpm }, 1);
        track.Segments[0].StepsPerBeat = stepsPerBeat;
        return track;
    }

    [Fact]
    public void EmptyTrack_EmitsOnlySpeed()
    {
        var events = MakeTrack().ToSequence().Events;

        // 120 BPM, four steps a beat - the 480 step rate stated as its two factors.
        Assert.Equal(["!speed", "!speed", "!divider"], events.Select(e => e.SoundEvent));
        Assert.Equal(120, events[0].Value);
        Assert.Equal(4, events[1].Value);
        Assert.Equal(ValueScale.Times, events[1].ValueScale);
    }

    [Fact]
    public void SingleNoteAtStepZero_FollowsSpeedDirectly()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!speed", "!divider", "boom"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void GapBeforeNote_EmitsStopWithGapLength()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 3, Instrument = Instrument.Single("boom") });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!speed", "!divider", "!stop", "boom"], events.Select(e => e.SoundEvent));
        Assert.Equal(3, events[3].Value);
    }

    [Fact]
    public void NotesOnSameStep_AreCombined()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("clap") });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!speed", "!divider", "boom", "!combine", "clap"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void NotesAddedOutOfOrder_AreSortedByStep()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("late") });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("early") });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!speed", "!divider", "early", "!stop", "late"], events.Select(e => e.SoundEvent));
        Assert.Equal(1, events[4].Value); // step 0 consumed one step, gap is 2 - 1
    }

    [Fact]
    public void MovingANote_ChangesItsPlaceInTheSequence()
    {
        var track = MakeTrack();
        var note = new Note { Step = 0, Instrument = Instrument.Single("boom") };
        track.Segments[0].Notes.Add(note);
        track.Segments[0].Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("clap") });

        note.Step = 5;
        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!speed", "!divider", "!stop", "clap", "!stop", "boom"],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void NoteValues_CarryIntoTheSequence()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note
            { Step = 0, Instrument = Instrument.Single("boom"), Value = 5, Volume = 60 });

        var ev = track.ToSequence().Events[3];

        Assert.Equal(5, ev.Value);
        Assert.Equal(60, ev.Volume);
        Assert.IsNotType<ExtendedEvent>(ev, false);
    }

    [Fact]
    public void PannedNote_BecomesAnExtendedEvent()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Pan = -50 });

        var ev = track.ToSequence().Events[3];

        var extended = Assert.IsType<ExtendedEvent>(ev);
        Assert.Equal(-50, extended.Pan);
    }

    [Fact]
    public void Placements_LandOnTheGrid()
    {
        // 120 BPM, 4 steps per beat -> sequence runs at 480 "BPM", one step = 0.125 s.
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("clap") });
        track.Segments[0].Notes.Add(new Note { Step = 6, Instrument = Instrument.Single("snare") });

        const uint sample_rate = 48000;
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = sample_rate });
        var placements = calculator.CalculateOne(track.ToSequence())
            .Where(p => p.Audible)
            .ToArray();

        const ulong step_samples = 6000; // 48000 / (480 / 60)
        var start = placements[0].Index;

        Assert.Equal(3, placements.Length);
        Assert.Equal(start, placements[1].Index); // combined with the first
        Assert.Equal(start + 6 * step_samples, placements[2].Index);
    }

    [Fact]
    public void TrackAutomation_WithSoundFilter_AppliesOnlyToMatchingNotes()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 4, Instrument = Instrument.Single("clap") });

        var manager = new AudioKeyframeManager();
        manager.Keyframes.Add(new AudioKeyframe { Gap = 2 });
        track.AddTrackAutomation(manager, ["boom"]);

        var events = track.ToSequence().Events;

        // boom (step 0) + its echo (step 2); clap (step 4) is untouched, no echo.
        Assert.Equal(["!speed", "!speed", "!divider", "boom", "!stop", "boom", "!stop", "clap"],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void TrackAutomation_WithNoSoundFilter_AppliesToEveryNote()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });
        track.Segments[0].Notes.Add(new Note { Step = 5, Instrument = Instrument.Single("clap") });

        var manager = new AudioKeyframeManager();
        manager.Keyframes.Add(new AudioKeyframe { Gap = 2 });
        track.AddTrackAutomation(manager); // Sounds == null -> every sound

        var events = track.ToSequence().Events;

        Assert.Equal(
            ["!speed", "!speed", "!divider", "boom", "!stop", "boom", "!stop", "clap", "!stop", "clap"],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void NoteWithOwnAutomation_AlsoGetsMatchingTrackAutomation()
    {
        var track = MakeTrack();
        var note = new Note { Step = 0, Instrument = Instrument.Single("boom") };
        note.Automation = new AudioKeyframeManager();
        note.Automation.Keyframes.Add(new AudioKeyframe { Gap = 2 }); // note-level echo at step 2
        track.Segments[0].Notes.Add(note);

        var trackManager = new AudioKeyframeManager();
        trackManager.Keyframes.Add(new AudioKeyframe { Gap = 4 }); // track-level echo at step 4
        track.AddTrackAutomation(trackManager);

        var events = track.ToSequence().Events;

        // Both automations fire independently from the same base note.
        Assert.Equal(["!speed", "!speed", "!divider", "boom", "!stop", "boom", "!stop", "boom"],
            events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void Transpose_ShiftsEveryGeneratedEventsValue_WithoutMutatingTheNote()
    {
        var track = MakeTrack();
        var note = new Note { Step = 0, Instrument = Instrument.Single("boom"), Value = 5 };
        note.Automation = new AudioKeyframeManager();
        note.Automation.Keyframes.Add(new AudioKeyframe { Gap = 2 }); // echo at step 2, same pitch
        track.Segments[0].Notes.Add(note);
        track.Transpose = -0.4f;

        var events = track.ToSequence().Events;

        Assert.Equal(4.6, events[3].Value, 5); // base note
        Assert.Equal(4.6, events[5].Value, 5); // its echo, also shifted
        Assert.Equal(5, note.Value); // the stored note is untouched
    }

    [Fact]
    public void SegmentAtGlobalStep_MapsAcrossSegmentBoundaries()
    {
        var track = MakeTrack(); // segment 0: 16 steps (4 bars * 4 steps/beat)
        var second = track.NewSegment();

        Assert.Equal((track.Segments[0], 0), track.SegmentAtGlobalStep(0));
        Assert.Equal((track.Segments[0], 15), track.SegmentAtGlobalStep(15));
        Assert.Equal((second, 0), track.SegmentAtGlobalStep(16));
        Assert.Equal((second, 5), track.SegmentAtGlobalStep(21));
        Assert.Null(track.SegmentAtGlobalStep(16 + second.StepCount));
    }

    [Fact]
    public void GlobalStepOf_IsTheInverseOfSegmentAtGlobalStep()
    {
        var track = MakeTrack();
        var second = track.NewSegment();
        var note = new Note { Step = 5, Instrument = Instrument.Single("boom") };
        second.Notes.Add(note);

        Assert.Equal(21, track.GlobalStepOf(second, note));
        Assert.Equal((second, 5), track.SegmentAtGlobalStep(21));
    }
}
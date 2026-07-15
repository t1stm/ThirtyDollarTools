using ThirtyDollarConverter.Objects;
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
        var sequence = MakeTrack(120, 4).ToSequence();

        var ev = Assert.Single(sequence.Events);
        Assert.Equal("!speed", ev.SoundEvent);
        Assert.Equal(480, ev.Value); // 120 BPM * 4 steps per beat
    }

    [Fact]
    public void SingleNoteAtStepZero_FollowsSpeedDirectly()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom" });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "boom"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void GapBeforeNote_EmitsStopWithGapLength()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 3, Sound = "boom" });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!stop", "boom"], events.Select(e => e.SoundEvent));
        Assert.Equal(3, events[1].Value);
    }

    [Fact]
    public void NotesOnSameStep_AreCombined()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom" });
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "clap" });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "boom", "!combine", "clap"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void NotesAddedOutOfOrder_AreSortedByStep()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 2, Sound = "late" });
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "early" });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "early", "!stop", "late"], events.Select(e => e.SoundEvent));
        Assert.Equal(1, events[2].Value); // step 0 consumed one step, gap is 2 - 1
    }

    [Fact]
    public void MovingANote_ChangesItsPlaceInTheSequence()
    {
        var track = MakeTrack();
        var note = new Note { Step = 0, Sound = "boom" };
        track.Segments[0].Notes.Add(note);
        track.Segments[0].Notes.Add(new Note { Step = 1, Sound = "clap" });

        note.Step = 5;
        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!stop", "clap", "!stop", "boom"], events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void NoteValues_CarryIntoTheSequence()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom", Value = 5, Volume = 60 });

        var ev = track.ToSequence().Events[1];

        Assert.Equal(5, ev.Value);
        Assert.Equal(60, ev.Volume);
        Assert.IsNotType<ExtendedEvent>(ev, false);
    }

    [Fact]
    public void PannedNote_BecomesAnExtendedEvent()
    {
        var track = MakeTrack();
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom", Pan = -50 });

        var ev = track.ToSequence().Events[1];

        var extended = Assert.IsType<ExtendedEvent>(ev);
        Assert.Equal(-50, extended.Pan);
    }

    [Fact]
    public void Placements_LandOnTheGrid()
    {
        // 120 BPM, 4 steps per beat -> sequence runs at 480 "BPM", one step = 0.125 s.
        var track = MakeTrack(120, 4);
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom" });
        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "clap" });
        track.Segments[0].Notes.Add(new Note { Step = 6, Sound = "snare" });

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
}
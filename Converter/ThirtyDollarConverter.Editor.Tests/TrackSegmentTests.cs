using ThirtyDollarConverter.Objects;

namespace ThirtyDollarConverter.Editor.Tests;

public class TrackSegmentTests
{
    private static ProjectTrack MakeTrack(float bpm = 120)
    {
        return new ProjectTrack(new TimingInfo { BPM = bpm }, 1);
    }

    [Fact]
    public void NewTrack_AlwaysHasOneCommonTimeSegment()
    {
        var segment = Assert.Single(MakeTrack().Segments);
        Assert.Equal(4, segment.Numerator);
        Assert.Equal(4, segment.Denominator);
    }

    [Fact]
    public void RemoveSegment_RefusesTheLastOne()
    {
        var track = MakeTrack();
        var only = track.Segments[0];

        Assert.False(track.RemoveSegment(only));
        Assert.Single(track.Segments);

        var second = track.NewSegment();
        Assert.True(track.RemoveSegment(second));
        Assert.Equal([only], track.Segments);
    }

    [Fact]
    public void SegmentBpmOverride_ChangesItsRealDuration()
    {
        var track = MakeTrack(); // 120 BPM
        var half_time = track.Segments[0]; // 4/4 bar, slowed to 60 BPM
        half_time.BPM = 60;
        half_time.StepsPerBeat = 1;

        var next = track.NewSegment();
        next.StepsPerBeat = 1;
        next.Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var events = track.ToSequence().Events;

        // The overridden bar is its own tempo region: 4 whole steps at !speed@60,
        // then the inherited 120 BPM quarter grid takes over with its own !speed.
        Assert.Equal(["!speed", "!divider", "!stop", "!speed", "boom"], events.Select(e => e.SoundEvent));
        Assert.Equal(60, events[0].Value);
        Assert.Equal(4, events[2].Value);
        Assert.Equal(120, events[3].Value);
    }

    [Fact]
    public void SecondSegment_StartsAfterTheFirstBar()
    {
        var track = MakeTrack(); // 4/4, 1 bar, 4 steps per beat -> 16 steps
        var second = track.NewSegment();
        second.Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!speed", "!divider", "!stop", "boom"], events.Select(e => e.SoundEvent));
        Assert.Equal(["!speed@120", "!speed@4@x"], events.Take(2).Select(e => e.Stringify()));
        Assert.Equal(16, events[3].Value); // one 4/4 bar of sixteenth steps
    }

    [Fact]
    public void SevenEight_BarLastsSevenEighths()
    {
        var track = MakeTrack();
        var seven = track.Segments[0];
        seven.Numerator = 7;
        seven.Denominator = 8;
        seven.StepsPerBeat = 1; // grid of eighth notes

        var next = track.NewSegment();
        next.StepsPerBeat = 1;
        next.Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var events = track.ToSequence().Events;

        // BPM stays anchored to the quarter note, so the eighth-note grid runs at
        // 240 steps/min at 120 BPM and the 7/8 bar takes exactly 7 of those steps;
        // the 4/4 quarter-grid segment after it becomes its own region.
        Assert.Equal(["!speed", "!speed", "!divider", "!stop", "!speed", "boom"], events.Select(e => e.SoundEvent));
        // 240 as "120 BPM, two steps a beat", then back down to the quarter grid.
        Assert.Equal(["!speed@120", "!speed@2@x"], events.Take(2).Select(e => e.Stringify()));
        Assert.Equal(7, events[3].Value);
        Assert.Equal("!speed@2@/", events[4].Stringify());
    }

    [Fact]
    public void ThreeTracksSwitchingTo78_ExportOnOneCleanGrid()
    {
        var project = new ThirtyDollarProject();
        var sounds = new[] { "kick", "snare", "hat" };

        for (var i = 0; i < 3; i++)
        {
            var track = project.NewTrack(); // segment 1: 4/4, sixteenth grid
            track.Segments[0].Notes.Add(new Note { Step = i * 4, Instrument = Instrument.Single(sounds[i]) });

            var odd = track.NewSegment(); // segment 2: 7/8, eighth grid
            odd.Numerator = 7;
            odd.Denominator = 8;
            odd.StepsPerBeat = 1;
            odd.Notes.Add(new Note { Step = i * 2 + 1, Instrument = Instrument.Single(sounds[i]) });

            project.Place(track, i, 0);
        }

        var events = project.ToSequence().Events;

        // Every track switches grid at the same bar line, so the export is exactly
        // two tempo regions: sixteenths at 480, then 7/8 eighths at 240.
        Assert.Equal(["!speed@120", "!speed@4@x", "!speed@2@/"],
            events.Where(e => e.SoundEvent == "!speed").Select(e => e.Stringify()));

        // And no fractional-stop or cancel hacks anywhere - every gap is whole steps.
        Assert.All(events.Where(e => e.SoundEvent == "!stop"),
            e => Assert.Equal(Math.Round(e.Value), e.Value));
        Assert.DoesNotContain(events, e => e.SoundEvent == "!combine");
    }

    [Fact]
    public void NotesAcrossASignatureChange_KeepTheirLocalGrid()
    {
        var track = MakeTrack();
        var common = track.Segments[0]; // 4/4, quarter-note grid
        common.StepsPerBeat = 1;
        common.Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("kick") });

        var odd = track.NewSegment(); // 7/8, eighth-note grid
        odd.Numerator = 7;
        odd.Denominator = 8;
        odd.StepsPerBeat = 1;
        odd.Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("snare") });

        var events = track.ToSequence().Events;

        // Each segment keeps its own grid: the kick counts quarters at 120/min, the
        // signature change flips the speed, and the snare counts eighths at 240/min.
        Assert.Equal(["!speed", "!divider", "!stop", "kick", "!stop", "!speed", "!stop", "snare"],
            events.Select(e => e.SoundEvent));
        Assert.Equal(120, events[0].Value);
        Assert.Equal(2, events[2].Value);
        Assert.Equal(1, events[4].Value); // kick advanced one step; one quarter remains
        Assert.Equal("!speed@2@x", events[5].Stringify()); // 240: the same 120 BPM, twice the grid
        Assert.Equal(1, events[6].Value);
    }

    [Fact]
    public void SpeedChange_KeepsPlacementsSampleExact()
    {
        // 120 BPM quarters (0.5 s/step), then a 240 BPM override (0.25 s/step).
        var track = MakeTrack();
        track.Segments[0].StepsPerBeat = 1;
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom") });

        var fast = track.NewSegment();
        fast.BPM = 240;
        fast.StepsPerBeat = 1;
        fast.Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        fast.Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("snare") });

        const uint sample_rate = 48000;
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = sample_rate });
        var placements = calculator.CalculateOne(track.ToSequence())
            .Where(p => p.Audible)
            .ToArray();

        var start = placements[0].Index;
        Assert.Equal(3, placements.Length);
        Assert.Equal(start + 4 * 24000ul, placements[1].Index); // one 4/4 bar of 0.5 s quarters
        Assert.Equal(start + 4 * 24000ul + 12000, placements[2].Index); // one 0.25 s step more
    }

    [Fact]
    public void StepPositionAt_TracksEachSegmentsOwnStepRate()
    {
        var track = MakeTrack(); // segment 0: 16 sixteenth steps at 120 BPM (1/480 min/step)
        var half_time = track.NewSegment(); // segment 1: quarter grid, 60 BPM (1/60 min/step)
        half_time.StepsPerBeat = 1;
        half_time.BPM = 60;

        Assert.Equal(-1, track.StepPositionAt(-1d / 480), 3); // one step before the track starts
        Assert.Equal(0, track.StepPositionAt(0), 3);
        Assert.Equal(8, track.StepPositionAt(1d / 60), 3); // 8 steps into segment 0
        Assert.Equal(16, track.StepPositionAt(1d / 30), 3); // segment 0 ends here
        Assert.Equal(16.5, track.StepPositionAt(1d / 24), 3); // half a step into segment 1
    }

    [Fact]
    public void NoteOnTheBoundary_LandsOnTheNextRegionsDownbeat()
    {
        // A note on its segment's end boundary (step == length, the BMS STOP case)
        // sounds exactly where the next region starts, after that region's !speed.
        var track = MakeTrack(); // 16 sixteenth steps at 480/min
        track.Segments[0].Notes.Add(new Note { Step = 16, Instrument = Instrument.Single("boom") });

        var next = track.NewSegment();
        next.StepsPerBeat = 1; // quarter grid, 120/min
        next.Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("kick") });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!speed", "!divider", "!stop", "!speed", "boom", "!stop", "kick"],
            events.Select(e => e.SoundEvent));
        Assert.Equal(["!speed@120", "!speed@4@x", "!stop@16", "!speed@4@/", "!stop@1"],
            events.Where(e => e.SoundEvent is "!speed" or "!stop").Select(e => e.Stringify()));
    }
}
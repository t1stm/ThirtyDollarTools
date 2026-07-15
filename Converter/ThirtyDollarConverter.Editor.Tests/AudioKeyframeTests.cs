using ThirtyDollarConverter.Objects;
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

        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom", Automation = echo });

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

        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom", Automation = slapback });

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
        track.Segments[0].Notes.Add(new Note { Step = 3, Sound = "boom", Automation = echo });

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

        track.Segments[0].Notes.Add(new Note { Step = 0, Sound = "boom", Automation = automation });

        var events = track.ToSequence().Events;

        var first = Assert.IsType<ExtendedEvent>(events[2]);
        Assert.Equal(80, first.Pan);
        Assert.Equal(0, first.Volume); // 100 - 200, floored at silence

        var second = Assert.IsType<ExtendedEvent>(events[3]);
        Assert.Equal(100, second.Pan); // 80 + 80, clamped to full right
        Assert.Equal(0, second.Volume);
    }

    [Fact]
    public void SharedManager_ExpandsEachNoteIndependently()
    {
        // The segment-level use case: one manager instance across many notes.
        var track = MakeTrack();
        var octave_up = new AudioKeyframeManager();
        octave_up.Keyframes.Add(new AudioKeyframe { Gap = 1, Value = new Modifier(12) });

        var notes = track.Segments[0].Notes;
        notes.Add(new Note { Step = 0, Sound = "harp", Value = 0, Automation = octave_up });
        notes.Add(new Note { Step = 8, Sound = "harp", Value = 7, Automation = octave_up });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "harp", "harp", "!stop", "harp", "harp"],
            events.Select(e => e.SoundEvent));
        // Each expansion starts from its own base note, not from shared state.
        Assert.Equal([0, 12, 7, 19],
            events.Where(e => e.SoundEvent == "harp").Select(e => e.Value));
    }
}
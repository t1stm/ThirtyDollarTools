using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor.Tests;

public class AudioKeyframeTests
{
    private static ProjectTrack MakeTrack()
    {
        return new ProjectTrack(new TimingInfo { BPM = 120 }, 1);
    }

    /// <summary>An automation that only places notes - the cuts are tested on their own.</summary>
    private static AudioKeyframeManager Silent(float gap, float end, AudioKeyframe? template = null)
    {
        return new AudioKeyframeManager
        {
            Cut = false,
            CutAtEnd = false,
            Gap = gap,
            Template = template ?? new AudioKeyframe(),
            End = end
        };
    }

    [Fact]
    public void Keyframes_FillTheGridBetweenTheNoteAndItsEnd()
    {
        var automation = Silent(1, 4);

        // Positions run one gap apart, the note's own start and its end excluded.
        Assert.Equal(3, automation.Keyframes.Count);
        Assert.Equal([1f, 2f, 3f], Enumerable.Range(0, 3).Select(automation.PositionOf));

        // A fractional end keeps every whole gap that still fits before it.
        automation.End = 3.5f;
        Assert.Equal([1f, 2f, 3f], Enumerable.Range(0, automation.Keyframes.Count).Select(automation.PositionOf));

        // A gap wider than the note leaves a long note with nothing inside it.
        automation.Gap = 8;
        Assert.Empty(automation.Keyframes);

        // And no length at all is a plain one-step note.
        automation.Gap = 1;
        automation.End = 0;
        Assert.Empty(automation.Keyframes);
    }

    [Fact]
    public void AutomationOffset_ShiftsTheWholeGrid()
    {
        // A voice that starts 2 steps after the one it must stay in phase with.
        var automation = Silent(4, 7);
        Assert.Equal([4f], Enumerable.Range(0, automation.Keyframes.Count).Select(automation.PositionOf));

        automation.AutomationOffset = -2;
        // Now on the earlier voice's instants: step 2 and 6 of the note = 4 and 8 of the track.
        Assert.Equal([2f, 6f], Enumerable.Range(0, automation.Keyframes.Count).Select(automation.PositionOf));

        // Positions at or before the note itself are not slots: they'd fire before it plays.
        automation.AutomationOffset = -4;
        Assert.Equal([4f], Enumerable.Range(0, automation.Keyframes.Count).Select(automation.PositionOf));
    }

    [Fact]
    public void Sync_KeepsEditedKeyframes_AndLosesTrimmedOnes()
    {
        var automation = Silent(1, 4, new AudioKeyframe { Volume = new Modifier(0.5, ModifierKind.Multiply) });

        // New keyframes clone the template, without taking its position.
        Assert.All(automation.Keyframes,
            keyframe => Assert.Equal(new Modifier(0.5, ModifierKind.Multiply), keyframe.Volume));
        Assert.All(automation.Keyframes, keyframe => Assert.Null(keyframe.Position));

        automation.Keyframes[1].Position = 2.5f;
        automation.Keyframes[1].Value = new Modifier(7);

        // Growing keeps what's there and appends template copies.
        automation.End = 6;
        Assert.Equal(5, automation.Keyframes.Count);
        Assert.Equal(2.5f, automation.PositionOf(1));
        Assert.Equal(new Modifier(7), automation.Keyframes[1].Value);
        Assert.Equal([2.5f, 3f, 4f, 5f], Enumerable.Range(1, 4).Select(automation.PositionOf));

        // Shortening drops from the tail; re-extending brings back a fresh template copy.
        automation.End = 2;
        Assert.Single(automation.Keyframes);
        automation.End = 6;
        Assert.Equal(new Modifier(0), automation.Keyframes[1].Value);
        Assert.Equal(2f, automation.PositionOf(1));
    }

    [Fact]
    public void StepKeyframes_GenerateADecayingEcho()
    {
        var track = MakeTrack(); // 4/4, sixteenth grid at 480 steps/min
        var echo = Silent(1, 3, new AudioKeyframe { Volume = new Modifier(0.5, ModifierKind.Multiply) });

        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Automation = echo });

        var events = track.ToSequence().Events;

        // One note becomes three, one grid step apart, each keyframe halving the last.
        Assert.Equal(["!speed", "!speed", "!divider", "boom", "boom", "boom"], events.Select(e => e.SoundEvent));
        Assert.Equal(["!speed@120", "!speed@4@x"], events.Take(2).Select(e => e.Stringify())); // 480 steps/min
        Assert.Null(events[3].Volume);
        Assert.Equal(50, events[4].Volume);
        Assert.Equal(25, events[5].Volume);
    }

    [Fact]
    public void PositionOverride_MovesOneKeyframeOnly()
    {
        var track = MakeTrack();
        var automation = Silent(4, 13);
        automation.Keyframes[1].Position = 6; // dragged off the grid, halfway back

        track.Segments[0].Notes.Add(new Note
            { Step = 0, Instrument = Instrument.Single("boom"), Automation = automation });

        var placements = new PlacementCalculator(new EncoderSettings { SampleRate = 48000 })
            .CalculateOne(track.ToSequence())
            .Where(p => p.Audible)
            .ToArray();

        // 4 steps = 0.5 s = 24000 samples: the note, then steps 4, 6 and 12.
        var starts = placements.Select(p => (double)p.Index - placements[0].Index).ToArray();
        Assert.Equal(4, starts.Length);
        Assert.InRange(starts[1], 23999, 24001);
        Assert.InRange(starts[2], 35999, 36001);
        Assert.InRange(starts[3], 71999, 72001);
    }

    [Fact]
    public void TimeKeyframes_PlaceExactlyBySeconds()
    {
        var track = MakeTrack();
        var slapback = Silent(0.007f, 0.01f); // 7 ms, off any musical grid
        slapback.Timing = KeyframeTiming.Time;

        track.Segments[0].Notes
            .Add(new Note { Step = 0, Instrument = Instrument.Single("boom"), Automation = slapback });

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
        var echo = Silent(0.6f, 1);
        echo.Timing = KeyframeTiming.Time;
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
        var automation = Silent(1, 3, new AudioKeyframe { Pan = new Modifier(80), Volume = new Modifier(-200) });
        automation.Keyframes[1].Volume = default; // the second one only pans

        track.Segments[0].Notes.Add(new Note
            { Step = 0, Instrument = Instrument.Single("boom"), Automation = automation });

        var events = track.ToSequence().Events;

        var first = Assert.IsType<ExtendedEvent>(events[4]);
        Assert.Equal(80, first.Pan);
        Assert.Equal(0, first.Volume); // 100 - 200, floored at silence

        var second = Assert.IsType<ExtendedEvent>(events[5]);
        Assert.Equal(100, second.Pan); // 80 + 80, clamped to full right
        Assert.Equal(0, second.Volume);
    }

    [Fact]
    public void OffsetKeyframes_WalkTheSoundStart_AndSurviveTheTextExport()
    {
        var track = MakeTrack();
        // The note starts 0.25 s into the sound; each keyframe jumps one beat later on
        // the grid AND another 0.5 s deeper into the sound (the scrub use case).
        var scrub = Silent(4, 12, new AudioKeyframe { Offset = new Modifier(0.5) });
        track.Segments[0].Notes.Add(new Note
            { Step = 0, Instrument = Instrument.Single("boom"), Offset = 0.25, Automation = scrub });

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
    public void CutKeyframe_CutsThenPlacesTheNoteAfterTheCut()
    {
        var track = MakeTrack(); // 4/4 sixteenth grid at 480 steps/min
        var cut = new AudioKeyframeManager { CutAtEnd = false, Gap = 4, End = 8 };

        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("loop"), Automation = cut });

        var events = track.ToSequence().Events;
        var cut_event = Assert.Single(events.OfType<IndividualCutEvent>());
        Assert.Equal(["loop"], cut_event.CutSounds);

        // The cut lands before the retriggered note in the same group.
        var cut_index = Array.IndexOf(events, cut_event);
        var retrigger_index = Array.IndexOf(events, events.Skip(cut_index + 1).First(e => e.SoundEvent == "loop"));
        Assert.True(cut_index < retrigger_index);

        const uint sample_rate = 48000;
        var calculator = new PlacementCalculator(new EncoderSettings { SampleRate = sample_rate });
        var placements = calculator.CalculateOne(track.ToSequence())
            .Where(p => p.Audible)
            .ToArray();

        // The base note, the cut, and the retriggered note - cut and retrigger share a
        // sample index (4 steps at 480 steps/min = 0.5 s = 24000 samples after the note).
        Assert.Equal(3, placements.Length);
        Assert.IsType<IndividualCutEvent>(placements[1].Event);
        Assert.Equal("loop", placements[2].Event.SoundEvent);
        Assert.InRange((double)placements[1].Index - placements[0].Index, 23999, 24001);
        Assert.Equal(placements[1].Index, placements[2].Index);
    }

    [Fact]
    public void CutAtEnd_SilencesTheNoteWhereItEnds()
    {
        var track = MakeTrack(); // 4/4 sixteenth grid at 480 steps/min
        var automation = new AudioKeyframeManager { Gap = 4, End = 12 };

        track.Segments[0].Notes.Add(new Note
            { Step = 0, Instrument = Instrument.Single("loop"), Automation = automation });

        // Two retriggers, each cut first, and one trailing cut where the note ends.
        var events = track.ToSequence().Events;
        Assert.Equal(3, events.Count(e => e.SoundEvent == "loop"));
        Assert.Equal(3, events.OfType<IndividualCutEvent>().Count());

        var placements = new PlacementCalculator(new EncoderSettings { SampleRate = 48000 })
            .CalculateOne(track.ToSequence())
            .Where(p => p.Audible)
            .ToArray();

        // The last cut is one gap past the last retrigger: 4 steps = 0.5 s = 24000 samples.
        var last = placements[^1];
        Assert.IsType<IndividualCutEvent>(last.Event);
        Assert.InRange((double)last.Index - placements[0].Index, 71999, 72001);

        // Without it the note rings on past its end.
        automation.CutAtEnd = false;
        Assert.Equal(2, track.ToSequence().Events.OfType<IndividualCutEvent>().Count());
    }

    /// <summary>
    ///     Several notes of one instrument retriggering on the same beat used to emit a cut
    ///     each, and every cut but the last silenced the note written before it.
    /// </summary>
    [Fact]
    public void CutKeyframes_OnOneStep_CutOnceBeforeTheWholeStep()
    {
        var track = MakeTrack(); // 4/4 sixteenth grid at 480 steps/min
        var chord = Instrument.Single("bleep");
        for (var value = 0; value < 3; value++)
        {
            var automation = new AudioKeyframeManager { CutAtEnd = false, Gap = 4, End = 12 };
            track.Segments[0].Notes.Add(new Note
                { Step = 0, Instrument = chord, Value = value, Automation = automation });
        }

        var events = track.ToSequence().Events
            .Select(e => e is IndividualCutEvent ? "!cut" : e.SoundEvent)
            .Where(name => name is not ("!speed" or "!divider" or "!combine"))
            .ToArray();

        // Three voices, two retriggers: one cut per retriggered step, ahead of its notes.
        Assert.Equal([
            "bleep", "bleep", "bleep",
            "!stop", "!cut", "bleep", "bleep", "bleep",
            "!stop", "!cut", "bleep", "bleep", "bleep"
        ], events);
    }

    /// <summary>
    ///     Cuts are per sound name, so a chord voice that starts later must retrigger on the
    ///     same instants as the voice it shares an instrument with - or its cuts silence the
    ///     other voice between its own retriggers. That is what the automation offset is for.
    /// </summary>
    [Fact]
    public void ChordVoices_LineUpOnTheSameInstants_ViaTheAutomationOffset()
    {
        var track = MakeTrack();
        var chord = Instrument.Single("bleep");
        track.Segments[0].Notes.Add(new Note
        {
            Step = 0, Instrument = chord, Value = 0,
            Automation = new AudioKeyframeManager { CutAtEnd = false, Gap = 4, End = 16 }
        });
        track.Segments[0].Notes.Add(new Note
        {
            Step = 2, Instrument = chord, Value = 7,
            Automation = new AudioKeyframeManager
                { CutAtEnd = false, Gap = 4, AutomationOffset = -2, End = 14 }
        });

        var events = track.ToSequence().Events
            .Select(e => e is IndividualCutEvent ? "!cut" : e.SoundEvent)
            .Where(name => name is not ("!speed" or "!divider" or "!combine"))
            .ToArray();

        // Step 0: the low voice. Step 2: the high one. Steps 4, 8 and 12: one cut, then
        // both voices - never a cut that lands between the two voices' retriggers.
        Assert.Equal([
            "bleep",
            "!stop", "bleep",
            "!stop", "!cut", "bleep", "bleep",
            "!stop", "!cut", "bleep", "bleep",
            "!stop", "!cut", "bleep", "bleep"
        ], events);
    }

    [Fact]
    public void Automation_SurvivesTheProjectFileRoundTrip_AndOnlyWritesEditedKeyframes()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var instrument = project.NewInstrument("boom");
        instrument.AddSound("boom");
        var echo = new AudioKeyframeManager
        {
            CutAtEnd = false,
            Gap = 1,
            AutomationOffset = -0.5f,
            Template = new AudioKeyframe { Offset = new Modifier(0.5) },
            End = 5
        };
        echo.Keyframes[2].Position = 3.25f;
        echo.Keyframes[2].Value = new Modifier(12);
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = instrument, Offset = 0.25, Automation = echo });

        var saved = ProjectFile.Save(project);
        var loaded = ProjectFile.Load(saved).Tracks[0].Segments[0].Notes[0].Automation!;

        Assert.True(echo.ValueEquals(loaded));
        Assert.Equal(5, loaded.Keyframes.Count);
        Assert.Equal(3.25f, loaded.PositionOf(2));
        Assert.Equal(new Modifier(0.5), loaded.Keyframes[0].Offset);

        // Only the one edited keyframe is written, not all five.
        Assert.Equal(1, saved.Split("\"index\"").Length - 1);
    }

    [Fact]
    public void SharedManager_ExpandsEachNoteIndependently()
    {
        // The segment-level use case: one manager instance across many notes.
        var track = MakeTrack();
        var octave_up = Silent(1, 2, new AudioKeyframe { Value = new Modifier(12) });

        var notes = track.Segments[0].Notes;
        notes.Add(new Note { Step = 0, Instrument = Instrument.Single("harp"), Value = 0, Automation = octave_up });
        notes.Add(new Note { Step = 8, Instrument = Instrument.Single("harp"), Value = 7, Automation = octave_up });

        var events = track.ToSequence().Events;

        Assert.Equal(["!speed", "!speed", "!divider", "harp", "harp", "!stop", "harp", "harp"],
            events.Select(e => e.SoundEvent));
        // Each expansion starts from its own base note, not from shared state.
        Assert.Equal([0, 12, 7, 19],
            events.Where(e => e.SoundEvent == "harp").Select(e => e.Value));
    }

    [Fact]
    public void Clone_IsIndependentOfTheOriginal()
    {
        var original = new AudioKeyframeManager
            { Timing = KeyframeTiming.Time, Gap = 1, Template = new AudioKeyframe { Value = new Modifier(12) }, End = 3 };

        var clone = original.Clone();
        clone.End = 9;
        clone.Keyframes[0].Position = 99;
        clone.Template.Value = new Modifier(1);

        Assert.Equal(3, original.End);
        Assert.Equal(2, original.Keyframes.Count);
        Assert.Null(original.Keyframes[0].Position);
        Assert.Equal(new Modifier(12), original.Template.Value);
    }

    [Fact]
    public void ValueEquals_ComparesStructureNotReference()
    {
        var a = new AudioKeyframeManager { Timing = KeyframeTiming.Time, Gap = 1, End = 3 };
        var clone = a.Clone();

        Assert.NotSame(a, clone);
        Assert.True(a.ValueEquals(clone));

        clone.End = 4;
        Assert.False(a.ValueEquals(clone));

        clone.End = 3;
        clone.Keyframes[0].Value = new Modifier(1);
        Assert.False(a.ValueEquals(clone));

        Assert.False(a.ValueEquals(new AudioKeyframeManager { Timing = KeyframeTiming.Time, Gap = 1 }));
    }
}

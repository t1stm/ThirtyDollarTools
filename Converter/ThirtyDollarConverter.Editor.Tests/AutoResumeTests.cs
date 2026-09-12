using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     A TDW cut is by sound name, so a long note is silenced by every other note's
///     retrigger guard that happens to name its sounds. Auto-resume puts it back: the cut
///     sounds are re-placed at the instant they were cut, continuing the sample where the
///     automation's auto offset asks for it. See docs/handover/editor-auto-resume-plan.md.
/// </summary>
public class AutoResumeTests
{
    /// <summary>A step lasts exactly one second, so a position in steps reads as a time in seconds.</summary>
    private const double StepMinutes = 1d / 60d;

    private static AudioKeyframeManager Long(float gap, float end, bool autoOffset = true)
    {
        // Template first: Gap and End resize the keyframe list from whatever it is then.
        return new AudioKeyframeManager
        {
            Template = new AudioKeyframe { AutoOffset = autoOffset },
            Gap = gap,
            End = end
        };
    }

    private static Note LongNote(int step, AudioKeyframeManager automation, Instrument? instrument = null,
        double value = 0)
    {
        return new Note
        {
            Step = step,
            Instrument = instrument ?? Instrument.Single("pad"),
            Value = value,
            Automation = automation
        };
    }

    /// <summary>The cuts one note puts on the timeline - what the other notes have to survive.</summary>
    private static List<CutPoint> CutsOf(Note note, bool generated = true)
    {
        return note.Automation!.Expand(note, note.Step * StepMinutes, StepMinutes)
            .Where(timed => timed.Event is IndividualCutEvent)
            .Select(timed => new CutPoint(timed.Minutes, ((IndividualCutEvent)timed.Event).CutSounds, generated))
            .ToList();
    }

    private static CutPoint Cut(double step, bool generated = true, params string[] sounds)
    {
        return new CutPoint(step * StepMinutes, new HashSet<string>(sounds.Length == 0 ? ["pad"] : sounds), generated);
    }

    private static GeneratedNote[] ResumesOf(Note note, IReadOnlyList<CutPoint> cuts)
    {
        return
        [
            .. note.Automation!.ExpandNotes(note, note.Step * StepMinutes, StepMinutes, cuts)
                .Where(generated => generated.KeyframeIndex < 0)
        ];
    }

    /// <summary>Every sound event the automation generates, as (step, sound, offset in seconds).</summary>
    private static (double Step, string Sound, double Offset)[] SoundsOf(Note note, IReadOnlyList<CutPoint>? cuts)
    {
        return
        [
            .. note.Automation!.Expand(note, note.Step * StepMinutes, StepMinutes, cuts)
                .Where(timed => timed.Event is not IndividualCutEvent)
                .Select(timed => (Math.Round(timed.Minutes / StepMinutes, 6), timed.Event.SoundEvent!,
                    (timed.Event as ExtendedEvent)?.OffsetInSeconds ?? 0))
        ];
    }

    [Fact]
    public void TwoLongNotes_AStepApart_ResumeThroughEachOthersCuts()
    {
        // The complaint: same gap, one step apart, so neither note's retriggers land on the
        // other's and every one of them silences the other note.
        var a = LongNote(0, Long(1.5f, 4.5f));
        var b = LongNote(1, Long(1.5f, 4.5f));

        // B retriggers at 2.5 and 4.0 and ends at 5.5 - none of them on A's grid of 1.5 / 3.0.
        Assert.Equal([2.5, 4.0, 5.5], CutsOf(b).Select(cut => Math.Round(cut.Minutes / StepMinutes, 6)));

        var sounds = SoundsOf(a, CutsOf(b));

        // A's own retriggers at 1.5 and 3.0, plus a resume at each of B's cuts inside A's
        // length. B's end cut at 5.5 is past A's end, so it is not repaired.
        Assert.Equal([1.5, 2.5, 3.0, 4.0], sounds.Select(sound => sound.Step));

        // Auto offset on: every instance is exactly as far into the sound as the note has
        // been playing, so the waveform runs on across both the retriggers and the resumes.
        Assert.Equal([1.5, 2.5, 3.0, 4.0], sounds.Select(sound => sound.Offset));

        // A resume adds no cut of its own - the cut it repairs is already at that instant.
        Assert.Equal([1.5, 3.0, 4.5], a.Automation!.Expand(a, 0, StepMinutes, CutsOf(b))
            .Where(timed => timed.Event is IndividualCutEvent)
            .Select(timed => Math.Round(timed.Minutes / StepMinutes, 6)));
    }

    [Fact]
    public void AlignedGrids_GenerateNoResumesAtAll()
    {
        // The hand-tuned case Grid offset already reaches: B's cuts land on A's own
        // retriggers, where HoistCuts merges them and every voice re-enters after the cut.
        var a = LongNote(0, Long(1.5f, 9f));
        var b = LongNote(3, Long(1.5f, 6f));

        Assert.Equal([4.5, 6.0, 7.5, 9.0], CutsOf(b).Select(cut => Math.Round(cut.Minutes / StepMinutes, 6)));

        // Nothing to repair, so the sequence is exactly what it is today - aligning the grids
        // stays the way to keep the exported event count down.
        Assert.Empty(ResumesOf(a, CutsOf(b)));
        Assert.Equal(SoundsOf(a, null), SoundsOf(a, CutsOf(b)));
    }

    [Fact]
    public void AUserCut_StopsTheSound_UntilTheNoteRetriggersItself()
    {
        var note = LongNote(0, Long(1.5f, 4.5f));
        // A cut the user placed is aimed at this sound on purpose; the generated one after it
        // is collateral from somebody else's automation.
        List<CutPoint> cuts = [Cut(2.5, false), Cut(4.0)];

        var resumes = ResumesOf(note, cuts);

        // Silent from 2.5 until the note's own retrigger at 3.0 brings it back; that instance
        // is then repaired like any other.
        var resume = Assert.Single(resumes);
        Assert.Equal(4.0, Math.Round(resume.Minutes / StepMinutes, 6));
    }

    [Fact]
    public void OnlyTheSoundsTheCutNamesComeBack()
    {
        var layered = new Instrument { Name = "layered" };
        layered.AddSound("pad");
        layered.AddSound("air");

        var note = LongNote(0, Long(1.5f, 4.5f), layered);
        var sounds = SoundsOf(note, [Cut(2.5, true, "pad")]);

        // The retrigger plays the instrument whole; the resume replaces only what was
        // silenced, because "air" is still ringing and must not be stacked on itself.
        Assert.Equal([(1.5, "pad", 1.5), (1.5, "air", 1.5), (2.5, "pad", 2.5), (3.0, "pad", 3.0), (3.0, "air", 3.0)],
            sounds);
    }

    [Fact]
    public void NothingOutsideTheNotesLengthIsResumed()
    {
        var note = LongNote(2, Long(1.5f, 4.5f));
        // Before the note, exactly on its end, and past it. The note runs 2 -> 6.5.
        Assert.Empty(ResumesOf(note, [Cut(1.5), Cut(6.5), Cut(8)]));

        // And a note with no declared length has nothing to be inside of: the library does
        // not know how long a sample rings, so a plain note is left as it is today.
        var plain = LongNote(0, Long(1.5f, 0f));
        Assert.Empty(ResumesOf(plain, [Cut(0.5)]));
    }

    [Fact]
    public void AutoOffsetOff_RestartsTheSoundInsteadOfSplicing()
    {
        // What the note's own retriggers already sound like: the sample from the top.
        var note = LongNote(0, Long(1.5f, 4.5f, false));
        Assert.Equal([(1.5, "pad", 0d), (2.5, "pad", 0d), (3.0, "pad", 0d)], SoundsOf(note, [Cut(2.5)]));

        // It is the ringing instance that decides, not the automation as a whole: this
        // keyframe restarts the sound, so the resume repairing it restarts too.
        var mixed = LongNote(0, Long(1.5f, 4.5f));
        mixed.Automation!.Keyframes[0].AutoOffset = false;
        Assert.Equal([(1.5, "pad", 0d), (2.5, "pad", 0d), (3.0, "pad", 1.5d)], SoundsOf(mixed, [Cut(2.5)]));
    }

    [Fact]
    public void BeforeTheFirstKeyframe_TheTemplateDecides()
    {
        // The stretch between the note and its first keyframe is played by the note itself,
        // which has no keyframe of its own to ask - the automation's template speaks for it.
        var note = LongNote(0, Long(1.5f, 4.5f));
        Assert.Equal([(1.0, "pad", 1.0), (1.5, "pad", 1.5), (3.0, "pad", 3.0)], SoundsOf(note, [Cut(1.0)]));

        var restarting = LongNote(0, Long(1.5f, 4.5f, false));
        Assert.Equal([(1.0, "pad", 0d), (1.5, "pad", 0d), (3.0, "pad", 0d)], SoundsOf(restarting, [Cut(1.0)]));
    }

    [Fact]
    public void TheElapsedTimeIsMeasuredAtThePitchTheSoundWasPlayingAt()
    {
        // An octave up eats the sample twice as fast, so one second of playing is two
        // seconds of sound - the same exponent AutoOffsetTests pins in the encoder.
        var note = LongNote(0, Long(1.5f, 4.5f), value: 12);
        Assert.Equal([(1.5, "pad", 3.0), (2.5, "pad", 5.0), (3.0, "pad", 6.0)], SoundsOf(note, [Cut(2.5)]));
    }

    // ---- Phase 2: the two-pass wiring ----

    /// <summary>
    ///     Two interfering long notes on one instrument, 480 steps a minute (120 BPM, four
    ///     steps a beat) so a step is 0.125 s.
    /// </summary>
    private static ThirtyDollarProject Interfering(out ProjectTrack track)
    {
        var project = new ThirtyDollarProject();
        track = project.NewTrack();
        var pad = project.NewInstrument("pad");
        pad.AddSound("pad");

        track.Segments[0].Notes.Add(LongNote(0, Long(1.5f, 4.5f), pad));
        track.Segments[0].Notes.Add(LongNote(1, Long(1.5f, 4.5f), pad));
        project.Place(track, 0, 0);
        return project;
    }

    /// <summary>Where each sound lands, in steps from the start of the sequence.</summary>
    private static double[] SoundSteps(Sequence sequence, string sound)
    {
        var step = 0d;
        var steps = new List<double>();
        foreach (var ev in sequence.Events)
        {
            if (ev.SoundEvent == "!stop") step += ev.Value;
            else if (ev.SoundEvent == "!combine") step -= 1;
            else if (ev.SoundEvent == sound) steps.Add(step);

            if (ev.SoundEvent is not null && !ev.SoundEvent.StartsWith('!') && ev is not ICustomActionEvent)
                step += 1;
        }

        return [.. steps];
    }

    [Fact]
    public void AProjectRepairsTheNotesItsOwnCutsSilence()
    {
        var project = Interfering(out _);

        // Both notes start, both retrigger on their own grid, and each is put back at every
        // instant the other's automation cut it: 1 + 1.5 = 2.5 and 1 + 3 = 4 repair the note
        // at step 0, and 1.5 / 3 / 4.5 repair the one at step 1.
        Assert.Equal([0, 1, 1.5, 1.5, 2.5, 2.5, 3, 3, 4, 4, 4.5], SoundSteps(project.ToSequence(), "pad"));

        // Off, the two notes cut each other exactly as they do today.
        project.AutoResume = false;
        Assert.Equal([0, 1, 1.5, 2.5, 3, 4], SoundSteps(project.ToSequence(), "pad"));
    }

    [Fact]
    public void AnIsolatedChannelResumesLikeTheMergedExport()
    {
        var project = Interfering(out var first);
        // The interfering voice moved to its own channel: the channel renders in isolation,
        // but the cross-channel cut injection means it still sees - and repairs - the cuts.
        var second = project.NewTrack();
        var pad = project.Instruments[0];
        second.Segments[0].Notes.Add(LongNote(1, Long(1.5f, 4.5f), pad));
        first.Segments[0].Notes.RemoveAt(1);
        project.Place(second, 1, 0);

        Assert.Equal([0, 1.5, 2.5, 3, 4], SoundSteps(project.ChannelSequence(0), "pad"));
        Assert.Equal([1, 1.5, 2.5, 3, 4, 4.5], SoundSteps(project.ChannelSequence(1), "pad"));

        // Which is exactly what the merged export plays, both channels together.
        Assert.Equal([0, 1, 1.5, 1.5, 2.5, 2.5, 3, 3, 4, 4, 4.5], SoundSteps(project.ToSequence(), "pad"));
    }

    [Fact]
    public void TheFlagSurvivesARoundTrip_AndAFileWithoutItIsOn()
    {
        var project = Interfering(out _);
        Assert.True(project.AutoResume);

        // On is the default, so it is not written at all - an untouched file stays byte-identical.
        var json = ProjectFile.Save(project);
        Assert.DoesNotContain("AutoResume", json);
        Assert.True(ProjectFile.Load(json).AutoResume);

        project.AutoResume = false;
        Assert.False(ProjectFile.Load(ProjectFile.Save(project)).AutoResume);
    }
}

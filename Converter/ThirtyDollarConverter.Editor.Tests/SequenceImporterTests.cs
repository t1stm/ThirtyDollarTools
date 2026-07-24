using ThirtyDollarParser;

namespace ThirtyDollarConverter.Editor.Tests;

public class SequenceImporterTests
{
    [Fact]
    public void RoundTrip_ThroughAddAsTrack_ReproducesTheOriginalSequenceText()
    {
        // A speed change plus combine/volume/pan/offset variety, so the walk is forced
        // to exercise region-splitting and every per-note bake, not just the easy path.
        var original = new ThirtyDollarProject();
        var track = original.NewTrack();
        track.Timing.BPM = 120;
        track.Segments[0].StepsPerBeat = 1;
        track.Segments[0].Notes.Add(new Note
        { Step = 0, Instrument = Instrument.Single("kick"), Volume = 80, Pan = -20, Offset = 0.1 });
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("hat") }); // combines
        track.Segments[0].Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("snare"), Value = 3 });

        var second = track.NewSegment();
        second.StepsPerBeat = 1;
        second.BPM = 90;
        second.Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("snare") });

        original.Place(track, 0, 0);

        var sequence = original.ToSequence();
        var target = new ThirtyDollarProject();
        SequenceImporter.AddAsTrack(target, sequence, "imported", null);

        Assert.Equal(SequenceText.Serialize(sequence), SequenceText.Serialize(target.ToSequence()));
    }

    [Fact]
    public void SpeedChange_ProducesOneSegmentPerRegion_AndARedundantSpeedSplitsNothing()
    {
        var sequence = Sequence.FromString("kick|!speed@150|snare|!speed@150|hat");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        Assert.Equal(2, result.Track!.Segments.Count);
        var first = result.Track.Segments[0];
        Assert.Equal(1, first.Numerator);
        Assert.Equal(300, first.BPM * first.StepsPerBeat); // rate = speed * k = 300 * 1
        var second = result.Track.Segments[1];
        Assert.Equal(150, second.BPM * second.StepsPerBeat); // rate = speed * k = 150 * 1
        Assert.Equal(2, second.Bars * second.Numerator * second.StepsPerBeat); // no padding needed
        Assert.Equal(2, second.Numerator); // small numerator beats a giant one for a 2-step region
    }

    [Fact]
    public void ExactFourBarFit_PrefersRealSubdivisionOverACoarseGrid()
    {
        var sequence = Sequence.FromString("kick=8"); // 8 steps, no speed change.
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var segment = result.Track!.Segments[0];
        // 2/4 time at 16th-note (SPB=4) resolution beats a quarter-note (SPB=1) grid.
        Assert.Equal(2, segment.Numerator);
        Assert.Equal(1, segment.Bars);
        Assert.Equal(4, segment.StepsPerBeat);
        Assert.Equal(75, segment.BPM);
        Assert.Equal(300, segment.BPM * segment.StepsPerBeat); // rate = speed * k = 300 * 1
        Assert.Equal(8, segment.Bars * segment.Numerator * segment.StepsPerBeat);
        Assert.Equal(8, segment.Notes.Count);
    }

    [Fact]
    public void PauseAndFractionalStop_AdvancePosition_AndPickTheMinimalSubdivision()
    {
        var sequence = Sequence.FromString("kick|_pause|snare|!stop@1.5|hat");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var segment = result.Track!.Segments[0];
        var notes = segment.Notes.OrderBy(n => n.Step).ToList();
        Assert.Equal(3, notes.Count); // "_pause" itself never becomes a note
        // Steps 0, 2, 4.5 need k=2 to land on an integer grid.
        Assert.Equal([0, 4, 9], notes.Select(n => n.Step));
        // 11 steps is prime - no clean bar fits, so the final region pads to 12
        // (trailing silence, never emitted by SequenceBuilder) with a small numerator.
        Assert.Equal(600, segment.BPM * segment.StepsPerBeat); // rate = speed * k = 300 * 2
        Assert.Equal(12, segment.Bars * segment.Numerator * segment.StepsPerBeat);
        Assert.True(segment.Numerator <= 16);
    }

    [Fact]
    public void UnrepresentableFraction_FallsBackToMaxSubdivision_AndCountsAQuantizedNote()
    {
        // 1/128 of a step can't land on any grid finer than 1/64 - guaranteed fallback.
        var sequence = Sequence.FromString("kick|!stop@0.0078125|snare");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        Assert.Equal(1, result.Warnings.QuantizedNotes);
    }

    [Fact]
    public void OneUnrepresentableOutlier_DoesNotForceTheRestOfTheRegionOntoAFinerGrid()
    {
        // "!stop@0.01" then "!stop@0.99" cancel out (net +1 step): only the note
        // sandwiched between them sits at an unrepresentable position (needs a
        // denominator of 100, past the 64 cap, like real "swing"/humanize timing).
        // Every other note is already whole at k=1 - real playback later walks this
        // segment with its own per-step INTEGER sample truncation, so needlessly
        // forcing a finer subdivision (e.g. the old "give up and use 64" fallback)
        // doesn't just fail to help the outlier - it also compounds truncation loss
        // across every other, already-exact note (reproduced against a real dropped
        // file: a blind k=64 fallback measured a 427ms drift by the end of a 96s
        // track; minimizing misalignment instead brought it down to ~2ms, bounded).
        var sequence = Sequence.FromString(
            "kick|kick|kick|!stop@0.01|snare|!stop@0.99|kick|kick|kick|kick|kick|kick");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        Assert.Equal(1, result.Warnings.QuantizedNotes);
        var segment = result.Track!.Segments[0];
        // stayed at native speed - k=1, not 64 (BPM alone may be scaled by StepsPerBeat now)
        Assert.Equal(300, segment.BPM * segment.StepsPerBeat);
    }

    [Fact]
    public void HighSpeedWithDenominator20Stops_PutsTheMultiplierInStepsPerBeat_NotBpm()
    {
        // a fast native speed plus fractional
        // stops whose true denominator (20) needs a k-search - the multiplier belongs in
        // StepsPerBeat, not baked into an absurd BPM.
        var sequence = Sequence.FromString("!speed@640|kick|!stop@0.95|snare|!stop@0.05|hat");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var segment = result.Track!.Segments[0];
        Assert.True(segment.BPM <= 640);
        Assert.True(segment.Numerator <= 16);
        Assert.Equal(640 * 20, segment.BPM * segment.StepsPerBeat); // rate = speed * k
    }

    [Fact]
    public void Combine_LayersMultipleSoundsOnOneStep_WithoutExtraStepAdvance()
    {
        var sequence = Sequence.FromString("kick|!combine|snare|!combine|hat|_pause");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var notes = result.Track!.Segments[0].Notes;
        Assert.Equal(3, notes.Count);
        Assert.All(notes, n => Assert.Equal(0, n.Step));
    }

    [Fact]
    public void Volume_BakesPerNote_PreservingNullOnlyAtGlobalDefault()
    {
        var sequence = Sequence.FromString("kick|!volume@50|snare|snare%80");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var notes = result.Track!.Segments[0].Notes.OrderBy(n => n.Step).ToList();
        Assert.Null(notes[0].Volume); // global 100, no explicit volume -> stays "follow sequence"
        Assert.Equal(50, notes[1].Volume); // global 50, no explicit volume -> (100) * 50 / 100
        Assert.Equal(40, notes[2].Volume); // global 50, explicit 80 -> 80 * 50 / 100
    }

    [Fact]
    public void Transpose_AddsToEveryFollowingNoteValue()
    {
        var sequence = Sequence.FromString("kick@2|!transpose@3|snare@1");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var notes = result.Track!.Segments[0].Notes.OrderBy(n => n.Step).ToList();
        Assert.Equal(2, notes[0].Value);
        Assert.Equal(4, notes[1].Value);
    }

    [Fact]
    public void Loop_FiresExactlyOnce_UnrollingTheLoopedSectionTwice()
    {
        var sequence = Sequence.FromString("!looptarget|snare|kick|!loop");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var sounds = result.Track!.Segments[0].Notes.OrderBy(n => n.Step)
            .Select(n => n.Instrument.Sounds[0]).ToArray();
        Assert.Equal(["snare", "kick", "snare", "kick"], sounds);
    }

    [Fact]
    public void Jump_SkipsEventsBetweenItAndItsTarget()
    {
        var sequence = Sequence.FromString("kick|!jump@1|snare|!target@1|hat");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var sounds = result.Track!.Segments[0].Notes.OrderBy(n => n.Step)
            .Select(n => n.Instrument.Sounds[0]).ToArray();
        Assert.Equal(["kick", "hat"], sounds);
    }

    [Fact]
    public void RunawayLoopMany_AbortsPastTheSafetyCap()
    {
        var sequence = Sequence.FromString("!looptarget|kick|!loopmany@99999999");
        var project = new ThirtyDollarProject();

        Assert.Throws<InvalidOperationException>(() => SequenceImporter.AddAsTrack(project, sequence, "test", null));
    }

    [Fact]
    public void SecondImport_OfTheSameName_GetsANumberedSuffix()
    {
        var sequence = Sequence.FromString("kick");
        var project = new ThirtyDollarProject();

        var first = SequenceImporter.AddAsTrack(project, sequence, "epic-sequence", null);
        var second = SequenceImporter.AddAsTrack(project, sequence, "epic-sequence", null);

        Assert.Equal("epic-sequence - imported", first.Track!.Name);
        Assert.Equal("epic-sequence - imported (2)", second.Track!.Name);
        Assert.Equal("kick - imported", first.Instruments[0].Name);
        Assert.Equal("kick - imported (2)", second.Instruments[0].Name);
    }

    [Fact]
    public void InstrumentDisplayName_ReplacesUnderscoresWithSpaces()
    {
        var sequence = Sequence.FromString("noteblock_harp");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        Assert.Equal("noteblock harp - imported", result.Instruments[0].Name);
    }

    [Fact]
    public void ToProject_SplitsIntoOneTrackPerSound_WithIndependentSegmentCopies()
    {
        var sequence = Sequence.FromString("kick|snare|kick");
        SequenceImporter.ToProject(sequence, "song", null, out var project);

        Assert.Equal(["kick - imported", "snare - imported"], project.Tracks.Select(t => t.Name));
        Assert.Equal([0, 1], project.Placements.Select(p => p.Channel).OrderBy(c => c));

        var kickSegment = project.Tracks[0].Segments[0];
        var snareSegment = project.Tracks[1].Segments[0];
        Assert.NotSame(kickSegment, snareSegment);
        Assert.Equal(2, kickSegment.Notes.Count); // steps 0 and 2
        Assert.Single(snareSegment.Notes); // step 1
    }

    [Fact]
    public void ToProject_RootBpm_FollowsTheFirstRegionsSpeed_OrDefaultsTo300()
    {
        var withoutSpeed = Sequence.FromString("kick");
        SequenceImporter.ToProject(withoutSpeed, "song", null, out var withoutSpeedProject);
        Assert.Equal(300, withoutSpeedProject.RootTiming.BPM);

        // A "!speed" set before any note never leaves an audible 300 BPM region behind.
        var withSpeed = Sequence.FromString("!speed@140|kick");
        SequenceImporter.ToProject(withSpeed, "song", null, out var withSpeedProject);
        Assert.Equal(140, withSpeedProject.RootTiming.BPM);
    }

    [Fact]
    public void UnknownSounds_AreStrippedAndReported()
    {
        var sequence = Sequence.FromString("kick|mystery_sound|snare");
        var project = new ThirtyDollarProject();
        var known = new HashSet<string> { "kick", "snare" };

        var result = SequenceImporter.AddAsTrack(project, sequence, "test", known);

        Assert.Equal(2, result.Track!.Segments[0].Notes.Count);
        Assert.Equal(["mystery_sound"], result.Warnings.UnknownSounds);
    }

    [Fact]
    public void UnsupportedEvents_AreIgnoredAndCounted()
    {
        var sequence = Sequence.FromString("kick|!divider|!divider|snare");
        var project = new ThirtyDollarProject();

        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        Assert.Equal(2, result.Warnings.IgnoredEvents["!divider"]);
    }

    [Fact]
    public void SequenceWithNoSounds_ThrowsInsteadOfImportingNothing()
    {
        var sequence = Sequence.FromString("!divider");
        var project = new ThirtyDollarProject();

        Assert.Throws<InvalidOperationException>(() => SequenceImporter.AddAsTrack(project, sequence, "test", null));
    }

    [Fact]
    public void IndividualCut_ImportsAsAPerSoundCutNote_OnThatSoundsOwnInstrument()
    {
        var sequence = Sequence.FromString("kick|!cut@kick|snare");
        var project = new ThirtyDollarProject();

        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var notes = result.Track!.Segments[0].Notes.OrderBy(n => n.Step).ToList();
        Assert.Equal(3, notes.Count);
        var cutNote = notes[1];
        Assert.True(cutNote.IsCut);
        Assert.Same(notes[0].Instrument, cutNote.Instrument); // the normal "kick" note's own instrument
        Assert.Null(cutNote.Volume);
        Assert.Equal(0, cutNote.Pan);
        Assert.Equal(0, cutNote.Offset);
    }

    [Fact]
    public void MultipleIndividualCuts_OnTheSameStep_EachImportAsTheirOwnCutNote()
    {
        // The exporter's pipe-repeated format ("!cut@a|!cut@b", not "!cut@a,b") means two
        // simultaneous per-sound cuts round-trip as two separate tokens, not one combined
        // multi-sound event - each maps to its own single-sound instrument, same as normal notes.
        var sequence = Sequence.FromString("kick|!cut@kick|!cut@snare|_pause");
        var project = new ThirtyDollarProject();

        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var cuts = result.Track!.Segments[0].Notes.Where(n => n.IsCut)
            .OrderBy(n => n.Instrument.Sounds[0]).ToList();
        Assert.Equal(2, cuts.Count);
        Assert.Equal(["kick", "snare"], cuts.Select(n => n.Instrument.Sounds[0]));
        Assert.All(cuts, n => Assert.Equal(1, n.Step)); // simultaneous: no advance between them
    }

    [Fact]
    public void BareGlobalCut_CutsEveryInstrumentIntroducedSoFar()
    {
        // A global cut can only be cutting sounds that were already triggered - "hat"
        // hasn't started yet, so it gets no cut note of its own.
        var sequence = Sequence.FromString("kick|snare|!cut|hat");
        var project = new ThirtyDollarProject();

        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var notes = result.Track!.Segments[0].Notes.OrderBy(n => n.Step).ToList();
        Assert.Equal(5, notes.Count); // kick, snare, cut(kick), cut(snare), hat
        var cuts = notes.Where(n => n.IsCut).OrderBy(n => n.Instrument.Sounds[0]).ToList();
        Assert.Equal(["kick", "snare"], cuts.Select(n => n.Instrument.Sounds[0]));
        Assert.All(cuts, n => Assert.Equal(2, n.Step));
        Assert.False(result.Warnings.IgnoredEvents.ContainsKey("!cut"));
    }

    [Fact]
    public void KnownSounds_FiltersCutsForUnknownSounds_SameAsNormalNotes()
    {
        var sequence = Sequence.FromString("kick|!cut@mystery|snare");
        var project = new ThirtyDollarProject();
        var known = new HashSet<string> { "kick", "snare" };

        var result = SequenceImporter.AddAsTrack(project, sequence, "test", known);

        Assert.Equal(2, result.Track!.Segments[0].Notes.Count); // the cut on an unknown sound is dropped
        Assert.Equal(["mystery"], result.Warnings.UnknownSounds);
    }

    [Fact]
    public void RepeatedIndividualCut_OnTheSameSound_CollapsesToOne()
    {
        var sequence = Sequence.FromString("kick|!cut@kick|!cut@kick|snare");
        var project = new ThirtyDollarProject();

        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        Assert.Equal(3, result.Track!.Segments[0].Notes.Count); // kick, one cut, snare
    }

    [Fact]
    public void ToProject_CutsShareTheirSoundsTrack_WithNormalNotesOfTheSameSound()
    {
        var sequence = Sequence.FromString("kick|!cut@kick|snare");
        SequenceImporter.ToProject(sequence, "song", null, out var project);

        Assert.Equal(["kick - imported", "snare - imported"], project.Tracks.Select(t => t.Name));
        var kickTrack = project.Tracks[0];
        Assert.Equal(2, kickTrack.Segments[0].Notes.Count); // the play at step 0 and the cut at step 1
        Assert.Contains(kickTrack.Segments[0].Notes, n => n.IsCut);
    }

    [Fact]
    public void RoundTrip_WithCutEvents_ReproducesTheOriginalSequenceText()
    {
        var original = new ThirtyDollarProject();
        var track = original.NewTrack();
        track.Timing.BPM = 120;
        track.Segments[0].StepsPerBeat = 1;
        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        track.Segments[0].Notes.Add(new Note { Step = 1, Instrument = Instrument.Single("kick"), IsCut = true });
        track.Segments[0].Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("kick"), IsCut = true });
        track.Segments[0].Notes.Add(new Note { Step = 2, Instrument = Instrument.Single("snare") }); // shares a step
        original.Place(track, 0, 0);

        var sequence = original.ToSequence();
        var target = new ThirtyDollarProject();
        SequenceImporter.AddAsTrack(target, sequence, "imported", null);

        Assert.Equal(SequenceText.Serialize(sequence), SequenceText.Serialize(target.ToSequence()));
    }
}

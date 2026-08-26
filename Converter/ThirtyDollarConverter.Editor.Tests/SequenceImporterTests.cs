using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Editor.Tests;

public class SequenceImporterTests
{
    /// <summary>
    ///     A stand-in for SampleHolder's map: every given ID reachable by ID, plus
    ///     any "id:emoji" pair reachable by both.
    /// </summary>
    private static Dictionary<string, Sound> SoundMap(params string[] ids)
    {
        var map = new Dictionary<string, Sound>();
        foreach (var id in ids)
        {
            var parts = id.Split(':');
            var sound = new Sound { Id = parts[0], Emoji = parts.Length > 1 ? parts[1] : null };
            map[sound.Id] = sound;
            if (sound.Emoji != null) map[sound.Emoji] = sound;
        }

        return map;
    }

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
    public void SpeedChangeByAWholeMultiple_StaysOneSegment_AtTheFinerGrid()
    {
        // 300 -> 150 is a subdivision change, not a tempo change: one segment on the 300
        // grid describes both halves, where splitting would leave two segments whose
        // awkward lengths force an unmusical StepsPerBeat.
        var sequence = Sequence.FromString("kick|!speed@150|snare|!speed@150|hat");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var segment = Assert.Single(result.Track!.Segments);
        Assert.Equal(300, segment.BPM * segment.StepsPerBeat); // grid rate is unchanged
        Assert.Equal(4, segment.Numerator);
        Assert.Equal(3, segment.Notes.Count);
        // "kick" at 300, then "snare"/"hat" a half-rate step apart: 0, 1, 3 on the 300 grid.
        Assert.Equal([0, 1, 3], segment.Notes.OrderBy(n => n.Step).Select(n => n.Step));
    }

    [Fact]
    public void SpeedChangeByAFractionalRatio_StillSplitsIntoItsOwnSegment()
    {
        // 300 -> 200 is 3:2. Merging would have to relabel one half's tempo, so a real
        // tempo change still earns its own segment - the counterpart to the test above.
        var sequence = Sequence.FromString("kick|!speed@200|snare|!speed@200|hat");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        Assert.Equal(2, result.Track!.Segments.Count);
        Assert.Equal(300, result.Track.Segments[0].BPM * result.Track.Segments[0].StepsPerBeat);
        Assert.Equal(200, result.Track.Segments[1].BPM * result.Track.Segments[1].StepsPerBeat);
    }

    [Fact]
    public void ExactFourBarFit_PrefersAMusicalSubdivisionAndAPlausibleTempo()
    {
        var sequence = Sequence.FromString("kick=8"); // 8 steps, no speed change.
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var segment = Assert.Single(result.Track!.Segments);
        // One 4/4 bar of 8th notes at 150 BPM, not a 2/4 bar of 16ths at 75 - same timing,
        // but 150 is a tempo somebody would write and 4/4 is the signature they'd reach for.
        Assert.Equal(4, segment.Numerator);
        Assert.Equal(1, segment.Bars);
        Assert.Equal(2, segment.StepsPerBeat);
        Assert.Equal(150, segment.BPM);
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

        var segment = Assert.Single(result.Track!.Segments);
        var notes = segment.Notes.OrderBy(n => n.Step).ToList();
        Assert.Equal(3, notes.Count); // "_pause" itself never becomes a note
        // Steps 0, 2, 4.5 need k=2 to land on an integer grid.
        Assert.Equal([0, 4, 9], notes.Select(n => n.Step));
        // 11 steps is prime - no clean bar fits, so the final region pads to 12
        // (trailing silence, never emitted by SequenceBuilder) rather than trailing a
        // short bar that would have to announce a tempo of its own.
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
    public void StopsWithANonMusicalDenominator_RoundOntoTheGrid_RatherThanWarpItToFitThem()
    {
        // Stops of 0.95/0.05 would sit exactly on a 20x grid - and 20 steps to a beat is a
        // grid nobody can edit against, bought at the cost of every other note's timing
        // (a finer grid multiplies PlacementCalculator's per-step sample truncation).
        // Rounding the one offending note and saying so is the better trade.
        var sequence = Sequence.FromString("!speed@640|kick|!stop@0.95|snare|!stop@0.05|hat");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var segment = Assert.Single(result.Track!.Segments);
        Assert.Equal(640, segment.BPM * segment.StepsPerBeat); // grid stays at the native speed
        Assert.Equal(2, segment.StepsPerBeat);
        Assert.Equal(1, result.Warnings.QuantizedNotes); // the 0.95 note, reported not hidden
        // 0, 1.95 and 3 steps: only the middle one moves, by 0.05 of a step (~4.7 ms here).
        Assert.Equal([0, 2, 3], segment.Notes.OrderBy(n => n.Step).Select(n => n.Step));
    }

    [Fact]
    public void AWholeMultipleChain_MergesToOneSegment_AtTheTempoTheFileNamed()
    {
        // The "!speed@2@x" flourish pattern that fragmented real sequences into dozens of
        // segments: 160 doubling to 320 and back is 8th notes at 160, not three tempos.
        var sequence = Sequence.FromString(
            "!speed@160|kick=4|!speed@2@x|snare=8|!speed@0.5@x|kick=4");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var segment = Assert.Single(result.Track!.Segments);
        Assert.Equal(160, segment.BPM); // the tempo the file actually named
        Assert.Equal(2, segment.StepsPerBeat); // the doubling lives here, not in the BPM
        Assert.Equal(4, segment.Numerator);
        Assert.Equal(3, segment.Bars);
        Assert.Equal(16, segment.Notes.Count);
        // Quarter notes at 160, then 8ths through the doubled section, then quarters again.
        Assert.Equal([0, 2, 4, 6, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 22],
            segment.Notes.OrderBy(n => n.Step).Select(n => n.Step));
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
            .Select(n => n.Instrument.Sounds[0].Sound).ToArray();
        Assert.Equal(["snare", "kick", "snare", "kick"], sounds);
    }

    [Fact]
    public void Jump_SkipsEventsBetweenItAndItsTarget()
    {
        var sequence = Sequence.FromString("kick|!jump@1|snare|!target@1|hat");
        var project = new ThirtyDollarProject();
        var result = SequenceImporter.AddAsTrack(project, sequence, "test", null);

        var sounds = result.Track!.Segments[0].Notes.OrderBy(n => n.Step)
            .Select(n => n.Instrument.Sounds[0].Sound).ToArray();
        Assert.Equal(["kick", "hat"], sounds);
    }

    [Fact]
    public void RunawayLoopMany_AbortsPastTheSafetyCap()
    {
        var sequence = Sequence.FromString("!looptarget|kick|!loopmany@99999999");
        var project = new ThirtyDollarProject();

        Assert.Throws<InvalidOperationException>(() => SequenceImporter.AddAsTrack(project, sequence, "test", null));
    }

    /// <summary>
    ///     Tracks are named apart; instruments are not, because a second import of the same
    ///     sound adopts the instrument the first one made rather than adding a twin beside it.
    ///     Retuning it then reaches both, which is the point of instruments being shared.
    /// </summary>
    [Fact]
    public void SecondImport_SuffixesTheTrackName_AndReusesTheInstrument()
    {
        var sequence = Sequence.FromString("kick");
        var project = new ThirtyDollarProject();

        var first = SequenceImporter.AddAsTrack(project, sequence, "epic-sequence", null);
        var second = SequenceImporter.AddAsTrack(project, sequence, "epic-sequence", null);

        Assert.Equal("epic-sequence - imported", first.Track!.Name);
        Assert.Equal("epic-sequence - imported (2)", second.Track!.Name);
        Assert.Equal("kick - imported", first.Instruments[0].Name);
        // Nothing new was added, so an undo of the second import has nothing to take back.
        Assert.Empty(second.Instruments);
        Assert.Single(project.Instruments);
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
        var known = SoundMap("kick", "snare");

        var result = SequenceImporter.AddAsTrack(project, sequence, "test", known);

        Assert.Equal(2, result.Track!.Segments[0].Notes.Count);
        Assert.Equal(["mystery_sound"], result.Warnings.UnknownSounds);
    }

    [Fact]
    public void EmojiSoundNames_AreImportedUnderTheirSoundId()
    {
        // A saved sequence writes "🍕", but labels can't draw an emoji - the ID
        // has to reach the instrument's name, its sounds and its cuts alike.
        var sequence = Sequence.FromString("🍕|!cut@🍕|pizza");
        var project = new ThirtyDollarProject();

        var result = SequenceImporter.AddAsTrack(project, sequence, "test", SoundMap("pizza:🍕"));

        var instrument = Assert.Single(result.Instruments);
        Assert.Equal("pizza - imported", instrument.Name);
        Assert.Equal(["pizza"], instrument.Sounds.Select(sound => sound.Sound));
        Assert.Empty(result.Warnings.UnknownSounds);
        Assert.All(result.Track!.Segments[0].Notes, note => Assert.Same(instrument, note.Instrument));
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
            .OrderBy(n => n.Instrument.Sounds[0].Sound).ToList();
        Assert.Equal(2, cuts.Count);
        Assert.Equal(["kick", "snare"], cuts.Select(n => n.Instrument.Sounds[0].Sound));
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
        var cuts = notes.Where(n => n.IsCut).OrderBy(n => n.Instrument.Sounds[0].Sound).ToList();
        Assert.Equal(["kick", "snare"], cuts.Select(n => n.Instrument.Sounds[0].Sound));
        Assert.All(cuts, n => Assert.Equal(2, n.Step));
        Assert.False(result.Warnings.IgnoredEvents.ContainsKey("!cut"));
    }

    [Fact]
    public void KnownSounds_FiltersCutsForUnknownSounds_SameAsNormalNotes()
    {
        var sequence = Sequence.FromString("kick|!cut@mystery|snare");
        var project = new ThirtyDollarProject();
        var known = SoundMap("kick", "snare");

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
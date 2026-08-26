using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor.Tests;

public class FaithfulTrackTests
{
    private const uint SampleRate = 48000;

    private static Instrument Layered(string name, params string[] sounds)
    {
        var instrument = new Instrument { Name = name };
        foreach (var sound in sounds) instrument.AddSound(sound);
        return instrument;
    }

    /// <summary>Sound placement offsets in samples, relative to the first sound.</summary>
    private static List<(string Sound, long Offset)> SoundTimings(Sequence sequence)
    {
        var placements = new PlacementCalculator(new EncoderSettings { SampleRate = SampleRate })
            .CalculateOne(sequence)
            .Where(p => p.Audible && !(p.Event.SoundEvent?.StartsWith('!') ?? true))
            .OrderBy(p => p.Index)
            .ToList();

        var origin = placements.Count == 0 ? 0ul : placements[0].Index;
        return [.. placements.Select(p => (p.Event.SoundEvent!, (long)p.Index - (long)origin))];
    }

    /// <summary>
    ///     The whole point of the kind: a faithful track must play exactly what
    ///     thirtydollar.website plays. Built as items, exported, and run through the real
    ///     engine - the timings have to land on the same samples as the raw sequence text,
    ///     loops unrolled and all. (Tolerance is the engine's own per-step integer sample
    ///     truncation, not slack in the walk.)
    /// </summary>
    [Fact]
    public void Timing_MatchesTheWebsiteEngine()
    {
        const string text = "kick|!speed@150|!stop@2|snare|!combine|hat|!looptarget|clap|!loopmany@2";

        var track = new FaithfulTrack(new TimingInfo(), 1);
        track.Items.AddRange([
            FaithfulItem.Sound(Instrument.Single("kick")),
            FaithfulItem.Parse("!speed@150")!,
            FaithfulItem.Parse("!stop@2")!,
            FaithfulItem.Sound(Layered("snare + hat", "snare", "hat")),
            FaithfulItem.Parse("!looptarget")!,
            FaithfulItem.Sound(Instrument.Single("clap")),
            FaithfulItem.Parse("!loopmany@2")!
        ]);

        var expected = SoundTimings(Sequence.FromString(text));
        var actual = SoundTimings(track.ToSequence());

        Assert.Equal(["kick", "snare", "hat", "clap", "clap", "clap"], actual.Select(t => t.Sound));
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Sound, actual[i].Sound);
            Assert.True(Math.Abs(expected[i].Offset - actual[i].Offset) <= 4,
                $"{actual[i].Sound} at {actual[i].Offset}, expected {expected[i].Offset}");
        }
    }

    /// <summary>
    ///     The playing view animates a slot per reported play, so every slot has to be
    ///     reported - the ones the walk consumes itself ("!speed", "!combine", "!looptarget",
    ///     "_pause", the loop family) included, exactly as PlacementCalculator's
    ///     AddVisualEvents reports them to the visualizer. A "!stop" reports once per step it
    ///     waits, a looped slot once per pass.
    /// </summary>
    [Fact]
    public void PlayTimes_ReportsEverySlot()
    {
        var track = new FaithfulTrack(new TimingInfo(), 1);
        track.Items.AddRange([
            FaithfulItem.Sound(Instrument.Single("kick")), // 0
            FaithfulItem.Parse("!speed@150")!, // 1
            FaithfulItem.Parse("!stop@2")!, // 2
            FaithfulItem.Sound(Layered("snare + hat", "snare", "hat")), // 3 snare, 4 !combine, 5 hat
            FaithfulItem.Parse("_pause")!, // 6
            FaithfulItem.Parse("!looptarget")!, // 7
            FaithfulItem.Sound(Instrument.Single("clap")), // 8
            FaithfulItem.Parse("!loopmany@2")! // 9
        ]);

        var played = track.PlayTimes().OrderBy(entry => entry.Minutes).ToList();
        var counts = played.CountBy(entry => entry.Index).ToDictionary();

        // Nothing drawn is silent on screen.
        Assert.Equal(Enumerable.Range(0, 10), counts.Keys.Order());

        Assert.Equal(2, counts[2]); // "!stop@2" - one fade per step waited
        Assert.Equal(3, counts[7]); // three passes over the loop body
        Assert.Equal(3, counts[8]);
        Assert.Equal(3, counts[9]);

        // The kick opens the sequence, and time only moves forward.
        Assert.Equal(0, played[0].Index);
        Assert.Equal(0, played[0].Minutes, 9);
        Assert.Equal(played.Select(entry => entry.Minutes).Order(), played.Select(entry => entry.Minutes));

        // The counter on the slot counts its passes down, as it does on the site.
        Assert.Equal([1, 0, 0], played.Where(entry => entry.Index == 9).Select(entry => entry.Event.WorkingValue));
    }

    /// <summary>
    ///     The clip's width on the arrangement grid: the walked length, loops unrolled.
    ///     Three claps at 150 plus the kick's step at 300 and the two-step stop.
    /// </summary>
    [Fact]
    public void DurationMinutes_UnrollsLoops()
    {
        var track = new FaithfulTrack(new TimingInfo(), 1);
        track.Items.AddRange([
            FaithfulItem.Parse("!looptarget")!,
            FaithfulItem.Sound(Instrument.Single("clap")),
            FaithfulItem.Parse("!loopmany@2")!
        ]);

        Assert.Equal(3 / 300d, track.DurationMinutes(), 9);
    }

    /// <summary>
    ///     A "_pause" item is how a faithful sequence leaves a gap: it advances the position a
    ///     step and plays nothing, so the sound after it lands one step later.
    /// </summary>
    [Fact]
    public void PauseItem_AdvancesAStepWithoutSounding()
    {
        var track = new FaithfulTrack(new TimingInfo(), 1);
        track.Items.AddRange([
            FaithfulItem.Sound(Instrument.Single("kick")),
            FaithfulItem.Parse("_pause")!,
            FaithfulItem.Sound(Instrument.Single("clap"))
        ]);

        var timings = SoundTimings(track.ToSequence());

        Assert.Equal(["kick", "clap"], timings.Select(t => t.Sound));
        // Two steps at the default speed of 300; the engine truncates each step to whole samples.
        Assert.True(Math.Abs(timings[1].Offset - 2 * SampleRate / 5) <= 4, $"clap at {timings[1].Offset}");
        Assert.Equal(3 / 300d, track.DurationMinutes(), 9);
    }

    [Fact]
    public void EmptyTrack_HasNoDurationAndStillExports()
    {
        var track = new FaithfulTrack(new TimingInfo(), 1);

        Assert.Equal(0, track.DurationMinutes());
        Assert.Equal(["!speed", "!divider"], track.ToSequence().Events.Select(e => e.SoundEvent));
    }

    /// <summary>
    ///     A bare "!cut" becomes a per-sound cut over everything the track plays: that is the
    ///     only cut form <see cref="ThirtyDollarProject" /> forwards to other channels, so an
    ///     isolated channel's preview cuts the same sounds the merged export does.
    /// </summary>
    [Fact]
    public void BareCut_SilencesEverySoundTheTrackPlays()
    {
        var track = new FaithfulTrack(new TimingInfo(), 1);
        track.Items.AddRange([
            FaithfulItem.Sound(Layered("kit", "kick", "hat")),
            FaithfulItem.Sound(Instrument.Single("clap")),
            FaithfulItem.Parse("!cut")!
        ]);

        var cut = Assert.Single(track.ToSequence().Events.OfType<IndividualCutEvent>());
        Assert.Equal(["clap", "hat", "kick"], cut.CutSounds.Order());
    }

    [Fact]
    public void SaveLoad_RoundTripsKindAndItems()
    {
        var project = new ThirtyDollarProject();
        var instrument = project.NewInstrument("bass");
        instrument.AddSound("boom");

        var track = (FaithfulTrack)project.NewTrack(TrackKind.Faithful);
        track.Items.AddRange([
            FaithfulItem.Sound(instrument),
            FaithfulItem.Parse("!speed@2@x")!,
            FaithfulItem.Parse("!bg@#ff8000,0.5")!
        ]);
        track.Items[0].Note!.Value = -3;
        track.Items[0].Note!.Volume = 70;
        project.Place(track, 0, 0);

        var loaded = ProjectFile.Load(ProjectFile.Save(project));
        var reloaded = Assert.IsType<FaithfulTrack>(Assert.Single(loaded.Tracks));

        Assert.Equal(TrackKind.Faithful, reloaded.Kind);
        Assert.Equal(3, reloaded.Items.Count);
        Assert.Equal("bass", reloaded.Items[0].Note!.Instrument.Name);
        Assert.Equal(-3, reloaded.Items[0].Note!.Value);
        Assert.Equal(70, reloaded.Items[0].Note!.Volume);
        Assert.Equal(ValueScale.Times, reloaded.Items[1].Action!.ValueScale);
        Assert.Equal(2, reloaded.Items[1].Action!.Value);
        Assert.Equal("!bg", reloaded.Items[2].Action!.SoundEvent);
        Assert.Equal(track.Items[2].Action!.Value, reloaded.Items[2].Action!.Value);

        // Same sequence out of both, so the round trip preserved timing too.
        Assert.Equal(SequenceText.Serialize(project.ToSequence()), SequenceText.Serialize(loaded.ToSequence()));
    }

    [Fact]
    public void PianoRollProjects_SaveWithoutAFaithfulKey()
    {
        var project = new ThirtyDollarProject();
        project.NewTrack();

        var json = ProjectFile.Save(project);

        Assert.DoesNotContain("\"kind\"", json);
        Assert.DoesNotContain("\"items\"", json);
    }

    [Fact]
    public void RemoveInstrument_RefusesWhileAFaithfulItemPlaysIt()
    {
        var project = new ThirtyDollarProject();
        var instrument = project.NewInstrument("bass");
        instrument.AddSound("boom");

        var track = (FaithfulTrack)project.NewTrack(TrackKind.Faithful);
        track.Items.Add(FaithfulItem.Sound(instrument));

        Assert.False(project.RemoveInstrument(instrument));

        track.Items.Clear();
        Assert.True(project.RemoveInstrument(instrument));
    }

    [Fact]
    public void DuplicateTrack_DeepCopiesItems()
    {
        var project = new ThirtyDollarProject();
        var instrument = project.NewInstrument("bass");
        instrument.AddSound("boom");

        var track = (FaithfulTrack)project.NewTrack(TrackKind.Faithful);
        track.Items.Add(FaithfulItem.Sound(instrument));

        var copy = Assert.IsType<FaithfulTrack>(project.DuplicateTrack(track, "copy"));
        copy.Items[0].Note!.Value = 12;

        Assert.Equal(0, track.Items[0].Note!.Value);
        // The instrument is a shared project resource, not owned by the item.
        Assert.Same(instrument, copy.Items[0].Note!.Instrument);
    }
}

using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     <see cref="SequenceImporter.AddAsFaithfulTrack" />: the import path a dropped sequence
///     takes when the faithful kind is picked, and the one both directions of the track-kind
///     conversion route through.
/// </summary>
public class FaithfulImportTests
{
    private const uint SampleRate = 48000;

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

    private static FaithfulTrack Import(ThirtyDollarProject project, string text)
    {
        return (FaithfulTrack)SequenceImporter
            .AddAsFaithfulTrack(project, Sequence.FromString(text), "dropped", null).Track!;
    }

    /// <summary>
    ///     The point of importing as this kind: nothing is fitted to a grid and nothing is
    ///     dropped, so the imported track plays the file back sample for sample.
    /// </summary>
    [Fact]
    public void Import_PlaysBackWhatWasImported()
    {
        const string text = "kick|!speed@150|!stop@2|snare|!combine|hat|_pause|clap|!looptarget|clap|!loopmany@2";

        var track = Import(new ThirtyDollarProject(), text);

        var expected = SoundTimings(Sequence.FromString(text));
        var actual = SoundTimings(track.ToSequence());

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Sound, actual[i].Sound);
            Assert.True(Math.Abs(expected[i].Offset - actual[i].Offset) <= 4,
                $"{actual[i].Sound} at {actual[i].Offset}, expected {expected[i].Offset}");
        }
    }

    /// <summary>
    ///     A "!combine" run is the one thing a faithful item models that the raw stream
    ///     doesn't: it collapses back into a single layered instrument, so the run is one
    ///     slot to click rather than three.
    /// </summary>
    [Fact]
    public void Import_CollapsesACombineRunIntoOneLayeredInstrument()
    {
        var project = new ThirtyDollarProject();

        var track = Import(project, "snare|!combine|hat|!combine|clap|kick");

        Assert.Equal(2, track.Items.Count);
        Assert.Equal(["snare", "hat", "clap"], track.Items[0].Note!.Instrument.Sounds.Select(s => s.Sound));
        Assert.Equal(["kick"], track.Items[1].Note!.Instrument.Sounds.Select(s => s.Sound));
    }

    /// <summary>Per-sound tuning inside a run rides on the instrument, relative to its first sound.</summary>
    [Fact]
    public void Import_KeepsRelativeTuning_OnTheInstrument()
    {
        var track = Import(new ThirtyDollarProject(), "kick@3|!combine|kick@-9");

        var item = Assert.Single(track.Items);
        Assert.Equal(3, item.Note!.Value);
        Assert.Equal([0, -12], item.Note.Instrument.Sounds.Select(s => s.Value));
    }

    /// <summary>Actions come through verbatim - that is the whole reason this kind exists.</summary>
    [Fact]
    public void Import_KeepsActionsVerbatim()
    {
        var track = Import(new ThirtyDollarProject(), "!speed@2@x|_pause|!divider|kick");

        Assert.Equal(["!speed", "_pause", "!divider"], track.Items.Take(3).Select(item => item.Action!.SoundEvent));
        Assert.Equal(ValueScale.Times, track.Items[0].Action!.ValueScale);
        Assert.NotNull(track.Items[3].Note);
    }

    /// <summary>An instrument the project already has is reused rather than duplicated beside itself.</summary>
    [Fact]
    public void Import_ReusesAMatchingInstrument()
    {
        var project = new ThirtyDollarProject();
        var existing = project.NewInstrument("kick");
        existing.AddSound("kick");

        var track = Import(project, "kick|kick");

        Assert.Same(existing, track.Items[0].Note!.Instrument);
        Assert.Same(existing, track.Items[1].Note!.Instrument);
        Assert.Single(project.Instruments);
    }

    /// <summary>
    ///     A sound the sample set doesn't know is reported and not played - but it still held
    ///     its step, so a "_pause" takes its place. Dropping it outright pulled every later
    ///     sound one step earlier, which is a whole cover slipping out of time.
    /// </summary>
    [Fact]
    public void Import_ReportsUnknownSounds_AndKeepsTheirSteps()
    {
        var project = new ThirtyDollarProject();
        var map = new Dictionary<string, Sound> { ["kick"] = new() { Id = "kick" } };

        var result = SequenceImporter.AddAsFaithfulTrack(project, Sequence.FromString("kick|nonsense|kick"), "x", map);
        var track = (FaithfulTrack)result.Track!;

        Assert.Equal(["nonsense"], result.Warnings.UnknownSounds);
        Assert.Equal(3, track.Items.Count);
        Assert.Equal("_pause", track.Items[1].Action!.SoundEvent);

        // The surviving kicks are still two steps apart, as they were in the file.
        var timings = SoundTimings(track.ToSequence());
        Assert.Equal(["kick", "kick"], timings.Select(t => t.Sound));
        Assert.Equal(SoundTimings(Sequence.FromString("kick|_pause|kick"))[1].Offset, timings[1].Offset);
    }

    /// <summary>
    ///     A "!combine"-joined run routinely carries its own per-sound "%volume"
    ///     ("rdclap@-4.4%30"), and a Note has one volume for the whole item. Rather than lose
    ///     them - or push them onto the instrument, which gave a real cover hundreds of
    ///     near-identical instruments - such a run stays a slot per sound.
    /// </summary>
    [Theory]
    [InlineData("snare|!combine|clap%30", 30d, 0f, 0d)] // volume
    [InlineData("snare|!combine|clap^0.5", null, 0.5f, 0d)] // pan
    [InlineData("snare|!combine|clap>0.18", null, 0f, 0.18)] // offset
    public void Import_SplitsARun_WhenItsSoundsDisagree(string text, double? volume, float pan, double offset)
    {
        var track = Import(new ThirtyDollarProject(), text);

        Assert.Equal(3, track.Items.Count);
        Assert.Equal("!combine", track.Items[1].Action!.SoundEvent);

        var second = track.Items[2].Note!;
        Assert.Equal(volume, second.Volume);
        Assert.Equal(pan, second.Pan);
        Assert.Equal(offset, second.Offset);
        Assert.Equal(["clap"], second.Instrument.Sounds.Select(s => s.Sound));
    }

    /// <summary>A run that does agree is one layered instrument, carrying it all on the note.</summary>
    [Fact]
    public void Import_KeepsAUniformRun_AsOneItem()
    {
        var track = Import(new ThirtyDollarProject(), "snare%30|!combine|clap%30");

        var item = Assert.Single(track.Items);
        Assert.Equal(30d, item.Note!.Volume);
        Assert.Equal(["snare", "clap"], item.Note.Instrument.Sounds.Select(s => s.Sound));
        Assert.All(item.Note.Instrument.Sounds, sound => Assert.Null(sound.Volume));
    }

    /// <summary>
    ///     A step's order is meaningful: a "!cut" silences the sounds written before it and
    ///     not the ones after. The export used to hoist a step's actions to its front, which
    ///     cut the wrong ones - and a faithful track is nothing but that order.
    /// </summary>
    [Fact]
    public void Import_KeepsACutInItsPlaceWithinAStep()
    {
        const string text = "snare|!combine|!cut@snare|!combine|clap|kick";

        var track = Import(new ThirtyDollarProject(), text);
        var exported = track.ToSequence().Events
            .Select(e => e is ThirtyDollarConverter.Parser.Custom_Events.IndividualCutEvent ? "!cut" : e.SoundEvent)
            .Where(name => name is not ("!speed" or "!divider" or "!combine"))
            .ToArray();

        Assert.Equal(["snare", "!cut", "clap", "kick"], exported);
    }

    /// <summary>All-or-nothing, same contract as the piano roll importer: nothing is added on a refusal.</summary>
    [Fact]
    public void Import_OfAnEmptySequence_LeavesTheProjectUntouched()
    {
        var project = new ThirtyDollarProject();

        Assert.Throws<InvalidOperationException>(() =>
            SequenceImporter.AddAsFaithfulTrack(project, Sequence.FromString(""), "x", null));

        Assert.Empty(project.Tracks);
        Assert.Empty(project.Instruments);
    }
}

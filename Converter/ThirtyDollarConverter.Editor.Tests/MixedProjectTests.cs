using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     The two track kinds in one project. A faithful track's grid rate is a raw TDW speed
///     that was never a tempo times a resolution, so putting one next to a piano-roll track
///     is the case where the merged export has to find a common grid or fall back to exact
///     fractional stops - and where per-channel playback could drift away from the export.
///     Everything here goes through the real <see cref="PlacementCalculator" />, so it is the
///     encoder's own opinion of when a sound plays, not the model's.
/// </summary>
public class MixedProjectTests
{
    private const uint SampleRate = 48000;

    /// <summary>
    ///     When every sound plays, in seconds from the first one. The encoder starts its clock
    ///     one step in, so absolute indices carry a constant lead-in that means nothing here.
    /// </summary>
    private static List<(string Sound, double Seconds)> SoundTimes(Sequence sequence)
    {
        var placements = new PlacementCalculator(new EncoderSettings { SampleRate = SampleRate })
            .CalculateOne(sequence)
            .Where(p => p.Audible && !(p.Event.SoundEvent?.StartsWith('!') ?? true))
            .OrderBy(p => p.Index)
            .ToList();

        if (placements.Count == 0) return [];

        var origin = placements[0].Index;
        return
        [
            .. placements.Select(p => (p.Event.SoundEvent!, ((long)p.Index - (long)origin) / (double)SampleRate))
        ];
    }

    private static void AssertTimes(IEnumerable<(string Sound, double Seconds)> expected,
        List<(string Sound, double Seconds)> actual)
    {
        var wanted = expected.ToList();
        Assert.Equal(wanted.Select(t => t.Sound), actual.Select(t => t.Sound));
        for (var i = 0; i < wanted.Count; i++)
            // A millisecond of slack for the encoder's per-step integer sample truncation.
            Assert.True(Math.Abs(wanted[i].Seconds - actual[i].Seconds) < 0.001,
                $"{actual[i].Sound} at {actual[i].Seconds:0.####}s, expected {wanted[i].Seconds:0.####}s");
    }

    /// <summary>
    ///     A 120 BPM sixteenth-grid pattern (480 steps a minute) and a faithful track at TDW's
    ///     default 300, overlapping on the timeline. 2400 is a whole multiple of both, so the
    ///     merged export lands every sound exactly rather than rounding either track onto the
    ///     other's grid.
    /// </summary>
    private static ThirtyDollarProject MixedProject()
    {
        var project = new ThirtyDollarProject { RootTiming = { BPM = 120 } };

        var kick = project.NewInstrument("kick");
        kick.AddSound("kick");
        var snare = project.NewInstrument("snare");
        snare.AddSound("snare");
        var hat = project.NewInstrument("hat");
        hat.AddSound("hat");

        // Piano roll: 4/4, four steps a beat -> one step is 0.125 s at 120 BPM.
        var drums = project.NewTrack();
        drums.Segments[0].Notes.Add(new Note { Step = 0, Instrument = kick });
        drums.Segments[0].Notes.Add(new Note { Step = 4, Instrument = snare }); // 0.5 s
        drums.Segments[0].Notes.Add(new Note { Step = 8, Instrument = kick }); // 1.0 s

        // Faithful: a hat, two steps of silence, another hat - 0.2 s a step at speed 300.
        var loop = (FaithfulTrack)project.NewTrack(TrackKind.Faithful);
        loop.Items.AddRange([
            FaithfulItem.Sound(hat),
            FaithfulItem.Parse("!stop@2")!,
            FaithfulItem.Sound(hat)
        ]);

        project.Place(drums, 0, 0);
        project.Place(loop, 1, 2); // two quarter notes at 120 BPM = 1 s in
        return project;
    }

    [Fact]
    public void MergedExport_PlaysBothKindsAtTheirOwnTimes()
    {
        var project = MixedProject();

        AssertTimes([
            ("kick", 0.0),
            ("snare", 0.5),
            ("kick", 1.0),
            ("hat", 1.0), // the faithful clip starts here
            ("hat", 1.6) // one step in, plus a two-step stop, at 0.2 s a step
        ], SoundTimes(project.ToSequence()));
    }

    /// <summary>
    ///     Editor playback renders each arrangement channel on its own and mixes the results,
    ///     so a channel's own sequence has to sit on the same timeline as the merged export -
    ///     including the silence before its first clip, which becomes a leading stop.
    /// </summary>
    [Fact]
    public void PerChannelPlayback_LandsOnTheSameTimesAsTheMergedExport()
    {
        var project = MixedProject();
        var merged = SoundTimes(project.ToSequence());

        // Channel 0 holds the piano roll and starts at 0, so its own origin is the merged one.
        AssertTimes(merged.Where(t => t.Sound != "hat"), SoundTimes(project.ChannelSequence(0)));

        // Channel 1's first sound is 1 s into the merged timeline; measured from its own first
        // sound, the gap between its two hats has to be the same 0.6 s.
        var faithful = SoundTimes(project.ChannelSequence(1));
        AssertTimes([("hat", 0.0), ("hat", 0.6)], faithful);
    }

    /// <summary>
    ///     Muting a channel must not move the other one. The merged render of the audible
    ///     channels is a different sequence from the full export, and a faithful track's raw
    ///     speed is exactly the kind of thing that could shift the common grid under it.
    /// </summary>
    [Fact]
    public void MutingTheFaithfulChannel_LeavesThePianoRollWhereItWas()
    {
        var project = MixedProject();

        AssertTimes([
            ("kick", 0.0),
            ("snare", 0.5),
            ("kick", 1.0)
        ], SoundTimes(project.ToSequence(channel => channel == 0)));
    }

    /// <summary>
    ///     A faithful track that lands on no common grid with its neighbour (301 is coprime
    ///     with the piano roll's 480, and 64 x 480 is the search ceiling) still has to play at
    ///     the right times - the builder falls back to exact fractional stops.
    /// </summary>
    [Fact]
    public void FaithfulSpeedWithNoCommonGrid_StillPlaysAtTheRightTimes()
    {
        var project = MixedProject();
        var loop = project.Tracks.OfType<FaithfulTrack>().Single();
        loop.Items.Insert(0, FaithfulItem.Parse("!speed@301")!);

        // One step is 60/301 s now: the second hat is three of them after the first.
        AssertTimes([
            ("kick", 0.0),
            ("snare", 0.5),
            ("kick", 1.0),
            ("hat", 1.0),
            ("hat", 1.0 + 3 * 60 / 301d)
        ], SoundTimes(project.ToSequence()));
    }

    /// <summary>
    ///     Save/load has to preserve a mixed project's timing exactly - the two kinds are
    ///     stored differently (segments and notes against items), and only the export proves
    ///     they came back the same.
    /// </summary>
    [Fact]
    public void MixedProject_SurvivesSaveAndLoad()
    {
        var project = MixedProject();
        var loaded = ProjectFile.Load(ProjectFile.Save(project));

        Assert.Equal(TrackKind.PianoRoll, loaded.Tracks[0].Kind);
        Assert.Equal(TrackKind.Faithful, loaded.Tracks[1].Kind);
        Assert.Equal(SequenceText.Serialize(project.ToSequence()), SequenceText.Serialize(loaded.ToSequence()));
    }
}

using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     The wave track: a WAVE file placed as a clip for reference while writing. It carries no
///     sounds and no tempo of its own, so everything here is about it staying invisible to the
///     sequence - the export must not notice it exists - while still being a real clip on the
///     arrangement, with a length the BPM stretches across the grid instead of resampling.
/// </summary>
public class WaveTrackTests
{
    private const uint SampleRate = 48000;

    private static WaveTrack AddWave(ThirtyDollarProject project, double seconds, string path = "/tmp/reference.wav")
    {
        var track = (WaveTrack)project.NewTrack(TrackKind.Wave);
        track.Path = path;
        track.DurationSeconds = seconds;
        return track;
    }

    /// <summary>
    ///     When the sequence's last real event lands, in seconds - audible or not, since the
    ///     playback padding's tail is a silent "_pause". The encoder sizes the rendered buffer
    ///     off the placement list, and that buffer is the transport's clock. The calculator's
    ///     own end marker (one step past the end) is dropped, so the number is the sequence's
    ///     content rather than its terminator.
    /// </summary>
    private static double LastPlacementSeconds(Sequence sequence)
    {
        var placements = new PlacementCalculator(new EncoderSettings { SampleRate = SampleRate })
            .CalculateOne(sequence)
            .Where(placement => placement.Event is not EndEvent)
            .ToList();

        return placements.Count == 0 ? 0 : (double)placements[^1].Index / SampleRate;
    }

    private static ProjectTrack OneBarQuarterGrid(ThirtyDollarProject project)
    {
        var track = project.NewTrack();
        track.Segments[0].StepsPerBeat = 1;
        return track;
    }

    [Fact]
    public void NewTrack_BuildsAWaveTrack()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack(TrackKind.Wave);

        Assert.IsType<WaveTrack>(track);
        Assert.Equal(TrackKind.Wave, track.Kind);
    }

    [Fact]
    public void DurationMinutes_IsTheFilesOwnLength_WhateverTheTempo()
    {
        var project = new ThirtyDollarProject { RootTiming = { BPM = 120 } };
        var wave = AddWave(project, 90);

        Assert.Equal(1.5, wave.DurationMinutes(), 9);

        // The file does not stretch - but the clip does, because the arrangement is anchored
        // in quarter notes: 1.5 minutes is 180 quarters at 120 BPM and 360 at 240.
        Assert.Equal(180, wave.DurationMinutes() * project.RootTiming.BPM, 9);
        project.RootTiming.BPM = 240;
        Assert.Equal(1.5, wave.DurationMinutes(), 9);
        Assert.Equal(360, wave.DurationMinutes() * project.RootTiming.BPM, 9);
    }

    [Fact]
    public void PlacedWaveTrack_ChangesNothingAboutTheExport()
    {
        var without = new ThirtyDollarProject();
        var withoutTrack = OneBarQuarterGrid(without);
        withoutTrack.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        without.Place(withoutTrack, 0, 0);

        var with = new ThirtyDollarProject();
        var withTrack = OneBarQuarterGrid(with);
        withTrack.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        with.Place(withTrack, 0, 0);
        // Longer than the pattern and starting before it, so a wave track that did leak into
        // the sequence would show up as a tempo region, a stop, or a longer timeline.
        with.Place(AddWave(with, 30), 1, 0);

        Assert.Equal(SequenceText.Serialize(without.ToSequence()), SequenceText.Serialize(with.ToSequence()));
    }

    [Fact]
    public void WaveTrack_ConvertsToAnEmptySequence()
    {
        var project = new ThirtyDollarProject();
        var wave = AddWave(project, 10);

        // Its own sequence is what per-track playback would render: nothing at all, not a
        // timeline of silence, and not a throw out of an empty region list.
        Assert.Empty(wave.ToSequence().Events);
        Assert.Empty(wave.ToSequence(new SequenceStyle { DividerEveryBars = 1 }).Events);
    }

    [Fact]
    public void WaveOnlyProject_BuildsAnEmptySequence()
    {
        // A reference dropped in before a single note exists: the merged timeline has no
        // regions at all, which used to index into the first track's empty region list.
        var project = new ThirtyDollarProject();
        project.Place(AddWave(project, 30), 0, 0);

        // Nothing but the tempo header an empty project already writes - no sounds, no grid
        // of its own. (Playback is the one build that pads the timeline out to the file's
        // end; see WaveOnlyProject_PlaysToTheEndOfTheFile.)
        var empty = SequenceText.Serialize(new ThirtyDollarProject().ToSequence());
        Assert.Equal(empty, SequenceText.Serialize(project.ToSequence()));
    }

    [Fact]
    public void WaveTrackFirstInTheList_DoesNotHideTheOtherTracksTempo()
    {
        // Ordering matters: the merged regions walk every placement's list, and the wave
        // track's is empty. Placed first, it must not become the timeline's opinion.
        var project = new ThirtyDollarProject();
        project.Place(AddWave(project, 30), 0, 0);
        var kick = OneBarQuarterGrid(project);
        kick.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        project.Place(kick, 1, 0);

        Assert.Contains("kick", project.ToSequence().Events.Select(e => e.SoundEvent));
    }

    [Fact]
    public void WaveTrack_PlaysNoInstrument()
    {
        var project = new ThirtyDollarProject();
        var instrument = project.NewInstrument("kick");
        instrument.AddSound("kick");
        AddWave(project, 5);

        Assert.True(project.RemoveInstrument(instrument));
    }

    [Fact]
    public void Duplicate_CopiesTheFileAndItsLength()
    {
        var project = new ThirtyDollarProject();
        var wave = AddWave(project, 12.5, "/music/loop.wav");
        wave.Volume = 60;

        var copy = Assert.IsType<WaveTrack>(project.DuplicateTrack(wave, "Reference copy"));

        Assert.Equal("Reference copy", copy.Name);
        Assert.Equal("/music/loop.wav", copy.Path);
        Assert.Equal(12.5, copy.DurationSeconds);
        Assert.Equal(60, copy.Volume);
    }

    [Fact]
    public void SaveLoad_KeepsTheFileLengthAndVolume()
    {
        var project = new ThirtyDollarProject();
        var wave = AddWave(project, 42.25, "/music/reference.wav");
        wave.Name = "Reference";
        wave.Volume = 75;
        project.Place(wave, 2, 8);

        var loaded = ProjectFile.Load(ProjectFile.Save(project));
        var track = Assert.IsType<WaveTrack>(Assert.Single(loaded.Tracks));

        Assert.Equal(TrackKind.Wave, track.Kind);
        Assert.Equal("Reference", track.Name);
        Assert.Equal("/music/reference.wav", track.Path);
        Assert.Equal(42.25, track.DurationSeconds);
        Assert.Equal(75, track.Volume);

        var placement = Assert.Single(loaded.Placements);
        Assert.Equal(2, placement.Channel);
        Assert.Equal(8, placement.StartQuarterNotes);
    }

    [Fact]
    public void SaveLoad_DefaultVolumeStaysFullAndIsNotWritten()
    {
        var project = new ThirtyDollarProject();
        AddWave(project, 1);

        var json = ProjectFile.Save(project);
        Assert.DoesNotContain("\"volume\"", json);
        Assert.Equal(100, Assert.IsType<WaveTrack>(ProjectFile.Load(json).Tracks[0]).Volume);
    }

    [Fact]
    public void WaveClip_ExtendsPlaybackPastTheLastNote_ButNotTheExport()
    {
        // The reference outlasts the four bars written against it so far. Playback has to run
        // to the end of the file - the rendered buffer is the transport's clock - while the
        // export stays what the site would play.
        var project = new ThirtyDollarProject();
        var kick = OneBarQuarterGrid(project);
        kick.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        project.Place(kick, 0, 0);
        project.Place(AddWave(project, 30), 1, 0);

        // 0.2 s is the TDW lead-in every sequence carries (see ChannelSequenceTests).
        Assert.Equal(30.2, LastPlacementSeconds(project.ToSequence(_ => true)), 2);
        Assert.Equal(0.2, LastPlacementSeconds(project.ToSequence()), 2);
    }

    [Fact]
    public void WaveOnlyProject_PlaysToTheEndOfTheFile()
    {
        // A reference dropped in before a single note exists still has to have a transport.
        var project = new ThirtyDollarProject();
        project.Place(AddWave(project, 30), 0, 0);

        Assert.Equal(30.2, LastPlacementSeconds(project.ToSequence(_ => true)), 2);
        Assert.Equal(30.2, LastPlacementSeconds(project.ChannelSequence(0)), 2);
    }

    [Fact]
    public void WavePadding_StartsWhereTheClipDoes()
    {
        var project = new ThirtyDollarProject { RootTiming = { BPM = 120 } };
        project.Place(AddWave(project, 30), 0, 8); // 8 quarters at 120 BPM = 4 s in

        Assert.Equal(34.2, LastPlacementSeconds(project.ToSequence(_ => true)), 2);
    }

    [Fact]
    public void WavePadding_SurvivesMutingAndSoloingItsChannel()
    {
        // Muting silences the clip's own buffer, not the timeline: the playhead must not
        // change length under a mute or a solo elsewhere.
        var project = new ThirtyDollarProject();
        var kick = OneBarQuarterGrid(project);
        kick.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        project.Place(kick, 0, 0);
        project.Place(AddWave(project, 30), 1, 0);

        Assert.Equal(30.2, LastPlacementSeconds(project.ToSequence(channel => channel == 0)), 2);
        Assert.Equal(30.2, LastPlacementSeconds(project.ChannelSequence(0)), 2);
    }

    [Fact]
    public void ReferenceShorterThanTheSong_ChangesNothing()
    {
        var project = new ThirtyDollarProject();
        var kick = OneBarQuarterGrid(project);
        kick.Segments[0].Notes.Add(new Note { Step = 0, Instrument = Instrument.Single("kick") });
        project.Place(kick, 0, 0);
        project.Place(kick, 0, 120); // a minute in at 120 BPM
        project.Place(AddWave(project, 5), 1, 0);

        Assert.Equal(SequenceText.Serialize(project.ToSequence()),
            SequenceText.Serialize(project.ToSequence(_ => true)));
    }
}

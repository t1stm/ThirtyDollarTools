using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Tests;

/// <summary>
///     <see cref="EditorState.ConvertTrack" />: the track context menu's "Convert to …".
///     A conversion goes through the track's own exported sequence, so what it has to keep is
///     everything that isn't in that sequence - the name, the colour, the list position and
///     the clips.
/// </summary>
public class TrackConversionTests
{
    private static Instrument MakeInstrument(EditorState state, string sound)
    {
        var instrument = state.AddInstrument(sound);
        state.SetInstrumentSounds(instrument, [new InstrumentSound { Sound = sound }]);
        return instrument;
    }

    /// <summary>Every sound the track plays, in order - what a conversion must not change.</summary>
    private static string[] Sounds(ProjectTrack track)
    {
        return [.. track.ToSequence().Events
            .Where(e => e.SoundEvent is { } sound && !sound.StartsWith('!') && sound != "_pause")
            .Select(e => e.SoundEvent!)];
    }

    private static ProjectTrack PianoRollTrack(EditorState state)
    {
        var track = state.AddTrack();
        state.AddNote(track.Segments[0], 0, MakeInstrument(state, "kick"), 0);
        state.AddNote(track.Segments[0], 4, MakeInstrument(state, "snare"), 2);
        return track;
    }

    [Fact]
    public void ConvertToFaithful_KeepsWhatTheSequenceDoesNotCarry()
    {
        var state = new EditorState();
        var before = state.AddTrack(); // so the converted one has to land at index 1, not the end
        var track = PianoRollTrack(state);
        state.RenameTrack(track, "Drums");
        state.SetTrackColor(track, 3);
        state.PlaceTrack(track, 2, 8);
        var sounds = Sounds(track);
        var duration = track.DurationMinutes();

        var converted = state.ConvertTrack(track);

        Assert.Equal(TrackKind.Faithful, converted.Kind);
        Assert.Equal("Drums", converted.Name);
        Assert.Equal(3, converted.ColorIndex);
        Assert.Equal([before, converted], state.Project.Tracks);
        Assert.Equal(sounds, Sounds(converted));
        // Not the duration: a TDW sequence has no trailing silence, so the converted clip
        // ends on its last sound rather than at the piano roll's segment boundary.
        Assert.True(converted.DurationMinutes() <= duration);

        var placement = Assert.Single(state.Project.Placements);
        Assert.Same(converted, placement.Track);
        Assert.Equal(2, placement.Channel);
        Assert.Equal(8, placement.StartQuarterNotes);
    }

    [Fact]
    public void ConvertToPianoRoll_KeepsTheSoundsAndTheClip()
    {
        var state = new EditorState();
        var track = (FaithfulTrack)state.AddTrack(TrackKind.Faithful);
        state.AppendItem(track, FaithfulItem.Sound(MakeInstrument(state, "kick")));
        state.AppendItem(track, FaithfulItem.Parse("!stop@2")!);
        state.AppendItem(track, FaithfulItem.Sound(MakeInstrument(state, "snare")));
        state.PlaceTrack(track, 1, 0);

        var converted = state.ConvertTrack(track);

        Assert.Equal(TrackKind.PianoRoll, converted.Kind);
        Assert.Equal(["kick", "snare"], Sounds(converted));
        Assert.Same(converted, Assert.Single(state.Project.Placements).Track);
    }

    [Fact]
    public void ConvertTrack_IsOneUndoEntry()
    {
        var state = new EditorState();
        var track = PianoRollTrack(state);
        var placement = state.PlaceTrack(track, 0, 4);
        var instruments = state.Project.Instruments.ToArray();

        state.ConvertTrack(track);
        state.Undo();

        Assert.Equal([track], state.Project.Tracks);
        Assert.Equal([placement], state.Project.Placements);
        Assert.Equal(instruments, state.Project.Instruments);

        state.Redo();
        Assert.Equal(TrackKind.Faithful, state.Project.Tracks[0].Kind);
    }

    /// <summary>The open panel has to follow the track it was showing, not a dropped one.</summary>
    [Fact]
    public void ConvertTrack_ReopensTheConvertedTrack_WhenTheOriginalWasOpen()
    {
        var state = new EditorState();
        var track = PianoRollTrack(state);
        state.SelectTrack(track);
        state.OpenTrack(track);

        var converted = state.ConvertTrack(track);

        Assert.Same(converted, state.OpenedTrack);
        Assert.Same(converted, state.SelectedTrack);
    }

    /// <summary>
    ///     A track with nothing in it has no sounds for the piano roll importer to place, so
    ///     that direction refuses - and the project has to survive the refusal untouched.
    /// </summary>
    [Fact]
    public void ConvertTrack_OfAnEmptyFaithfulTrack_Throws_AndChangesNothing()
    {
        var state = new EditorState();
        var track = state.AddTrack(TrackKind.Faithful);

        Assert.Throws<InvalidOperationException>(() => state.ConvertTrack(track));
        Assert.Equal([track], state.Project.Tracks);
        Assert.Empty(state.Project.Instruments);
    }
}

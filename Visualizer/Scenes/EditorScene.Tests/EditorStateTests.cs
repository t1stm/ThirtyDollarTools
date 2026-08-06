using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Tests;

public class EditorStateTests
{
    private static Instrument MakeInstrument(EditorState state, string sound)
    {
        var instrument = state.AddInstrument(sound);
        state.SetInstrumentSounds(instrument, [new InstrumentSound { Sound = sound }]);
        return instrument;
    }

    [Fact]
    public void AddTrack_MutatesProject_SetsDirty_AndNotifies()
    {
        var state = new EditorState();
        var changed = 0;
        state.OnProjectChanged += () => changed++;

        var track = state.AddTrack();

        Assert.Equal([track], state.Project.Tracks);
        Assert.True(state.Dirty);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void RemoveTrack_ClearsSelection_WhenTheSelectedTrackGoes()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        state.SelectTrack(track);

        var selected = track;
        state.OnSelectionChanged += t => selected = t;

        Assert.True(state.RemoveTrack(track));
        Assert.Null(state.SelectedTrack);
        Assert.Null(selected);
        Assert.False(state.RemoveTrack(track));
    }

    [Fact]
    public void RenameTrack_ToTheSameName_DoesNotDirty()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        state.SaveProject(); // clears dirty

        state.RenameTrack(track, track.Name);
        Assert.False(state.Dirty);

        state.RenameTrack(track, "Drums");
        Assert.Equal("Drums", track.Name);
        Assert.True(state.Dirty);
    }

    [Fact]
    public void Edit_DirtiesAndNotifies()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        state.SaveProject(); // clears dirty
        var fired = 0;
        state.OnProjectChanged += () => fired++;

        state.Edit(() => track.Segments[0].Bars = 3);

        Assert.Equal(3, track.Segments[0].Bars);
        Assert.True(state.Dirty);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void TrackTimingFollow_SwapsTheInstance_AndSurvivesSaveLoad()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        Assert.True(state.TrackFollowsRootTiming(track));

        // Same value: no dirty, no event.
        state.SaveProject();
        var fired = 0;
        state.OnProjectChanged += () => fired++;
        state.SetTrackFollowsRootTiming(track, true);
        Assert.False(state.Dirty);
        Assert.Equal(0, fired);

        // Unfollow copies the current values; root edits stop reaching the track.
        state.SetTrackFollowsRootTiming(track, false);
        Assert.False(state.TrackFollowsRootTiming(track));
        var ownBpm = track.Timing.BPM;
        state.Edit(() => state.Project.RootTiming.BPM = 199);
        Assert.Equal(ownBpm, track.Timing.BPM);

        // The own timing survives the save format's null-timing convention.
        state.LoadProject(state.SaveProject());
        var loaded = state.Project.Tracks[0];
        Assert.False(state.TrackFollowsRootTiming(loaded));
        Assert.Equal(ownBpm, loaded.Timing.BPM);

        // Following again shares the instance, so root edits reach the track.
        state.SetTrackFollowsRootTiming(loaded, true);
        state.Edit(() => state.Project.RootTiming.BPM = 90);
        Assert.Equal(90, loaded.Timing.BPM);
    }

    [Fact]
    public void SelectTrack_FiresOncePerChange()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var fired = 0;
        state.OnSelectionChanged += _ => fired++;

        state.SelectTrack(track);
        state.SelectTrack(track);

        Assert.Equal(1, fired);
        Assert.Same(track, state.SelectedTrack);
    }

    [Fact]
    public void SaveAndLoad_RoundTripThroughProjectFile_AndResetDirtyAndSelection()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        state.RenameTrack(track, "Lead");
        state.SelectTrack(track);

        var json = state.SaveProject();
        Assert.False(state.Dirty);

        var loaded = new EditorState();
        loaded.AddTrack(); // pre-existing content gets replaced
        loaded.LoadProject(json);

        Assert.False(loaded.Dirty);
        Assert.Null(loaded.SelectedTrack);
        Assert.Equal("Lead", Assert.Single(loaded.Project.Tracks).Name);
    }

    [Fact]
    public void PlacementLifecycle_MutatesDirtiesAndNotifies()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        state.SaveProject(); // clears dirty

        var changed = 0;
        state.OnProjectChanged += () => changed++;

        var placement = state.PlaceTrack(track, 2, 8);
        Assert.Equal([placement], state.Project.Placements);
        Assert.True(state.Dirty);

        state.MovePlacement(placement, 2, 8); // no-op move must not dirty again
        Assert.Equal(1, changed);

        state.MovePlacement(placement, 3, 12);
        Assert.Equal(3, placement.Channel);
        Assert.Equal(12, placement.StartQuarterNotes);
        Assert.Equal(2, changed);

        state.SelectPlacement(placement);
        Assert.True(state.RemovePlacement(placement));
        Assert.Null(state.SelectedPlacement);
        Assert.False(state.RemovePlacement(placement));
        Assert.Equal(3, changed);
    }

    [Fact]
    public void RemoveTrack_ClearsTheSelectedPlacementOfThatTrack()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 0, 0);
        state.SelectPlacement(placement);

        Assert.True(state.RemoveTrack(track));

        Assert.Null(state.SelectedPlacement);
        Assert.Empty(state.Project.Placements);
    }

    [Fact]
    public void ChannelMuteSolo_FollowsFlSemantics_AndNotifies()
    {
        var state = new EditorState();
        var fired = 0;
        state.OnChannelsChanged += () => fired++;

        Assert.True(state.IsChannelAudible(0));

        state.ToggleMute(0);
        Assert.True(state.IsMuted(0));
        Assert.False(state.IsChannelAudible(0));
        Assert.True(state.IsChannelAudible(1));

        // Any solo wins over everything, including un-muted channels.
        state.ToggleSolo(2);
        Assert.True(state.IsSoloed(2));
        Assert.True(state.IsChannelAudible(2));
        Assert.False(state.IsChannelAudible(1));

        state.ToggleSolo(2);
        state.ToggleMute(0);
        Assert.True(state.IsChannelAudible(0));
        Assert.Equal(4, fired);
        Assert.False(state.Dirty); // session-only: never part of the saved project
    }

    [Fact]
    public void LoadingAProject_ClearsChannelState()
    {
        var state = new EditorState();
        state.ToggleMute(1);
        state.ToggleSolo(2);

        state.LoadProject(state.SaveProject());

        Assert.False(state.IsMuted(1));
        Assert.True(state.IsChannelAudible(1));
        Assert.False(state.IsSoloed(2));
    }

    [Fact]
    public void OpenTrack_SelectsItsFirstSegment_AndSeedsTheActiveInstrument()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        state.AddNote(track.Segments[0], 3, boom, 5);

        ProjectTrack? opened = null;
        state.OnOpenedTrackChanged += t => opened = t;

        state.OpenTrack(track);
        Assert.Same(track, state.OpenedTrack);
        Assert.Same(track, opened);
        Assert.Same(track.Segments[0], state.SelectedSegment);
        Assert.Same(boom, state.ActiveInstrument);

        state.CloseTrack();
        Assert.Null(state.OpenedTrack);
        Assert.Null(state.SelectedSegment);
        Assert.Null(opened);
    }

    [Fact]
    public void OpenTrack_RemembersEachTracksLastActiveInstrument()
    {
        var state = new EditorState();
        var trackA = state.AddTrack();
        var trackB = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        var click = MakeInstrument(state, "click");
        state.AddNote(trackB.Segments[0], 0, click, 0);

        // Track A starts empty: no notes to seed from, so it opens with null.
        state.OpenTrack(trackA);
        Assert.Null(state.ActiveInstrument);

        // Picking an instrument while A is open is remembered for A specifically.
        state.ActiveInstrument = boom;

        // Track B has never been opened: seeds from its first note.
        state.OpenTrack(trackB);
        Assert.Same(click, state.ActiveInstrument);

        // Switching back to A restores what was last active there, not B's instrument.
        state.OpenTrack(trackA);
        Assert.Same(boom, state.ActiveInstrument);
    }

    [Fact]
    public void NoteLifecycle_MutatesDirtiesAndNotifies()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        state.SaveProject(); // clears dirty

        var changed = 0;
        state.OnProjectChanged += () => changed++;

        var note = state.AddNote(segment, 4, boom, -3);
        Assert.Equal([note], segment.Notes);
        Assert.Equal(4, note.Step);
        Assert.Equal(-3, note.Value);
        Assert.True(state.Dirty);

        state.MoveNote(segment, segment, note, 4, -3); // no-op move must not dirty again
        Assert.Equal(1, changed);

        var second = state.AddSegment(track);
        state.MoveNote(segment, second, note, 1, 2.4);
        Assert.Empty(segment.Notes);
        Assert.Equal([note], second.Notes);
        Assert.Equal(1, note.Step);
        Assert.Equal(2.4, note.Value);

        state.SelectNote(note);
        Assert.True(state.RemoveNote(second, note));
        Assert.Null(state.SelectedNote);
        Assert.False(state.RemoveNote(second, note));
    }

    [Fact]
    public void RemoveSegment_RespectsTheLastSegmentInvariant_AndFixesSelection()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var second = state.AddSegment(track);
        var note = state.AddNote(second, 0, MakeInstrument(state, "boom"), 0);
        state.OpenTrack(track);
        state.SelectSegment(second);
        state.SelectNote(note);

        Assert.True(state.RemoveSegment(track, second));
        Assert.Same(track.Segments[0], state.SelectedSegment);
        Assert.Null(state.SelectedNote);

        Assert.False(state.RemoveSegment(track, track.Segments[0])); // never below one segment
        Assert.Single(track.Segments);
    }

    [Fact]
    public void RemovingTheOpenedTrack_ClosesTheNoteEditor()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        state.OpenTrack(track);

        Assert.True(state.RemoveTrack(track));
        Assert.Null(state.OpenedTrack);
    }

    [Fact]
    public void LoadingAProject_ClosesTheNoteEditor_AndClearsTheActiveInstrument()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        state.ActiveInstrument = MakeInstrument(state, "boom");
        state.OpenTrack(track);

        state.LoadProject(state.SaveProject());

        Assert.Null(state.OpenedTrack);
        Assert.Null(state.SelectedSegment);
        Assert.Null(state.SelectedNote);
        Assert.Null(state.ActiveInstrument);
    }

    [Fact]
    public void InstrumentLifecycle_MutatesDirtiesAndNotifies()
    {
        var state = new EditorState();
        state.SaveProject(); // clears dirty

        var changed = 0;
        state.OnInstrumentsChanged += () => changed++;

        var instrument = state.AddInstrument("Layer");
        Assert.Equal([instrument], state.Project.Instruments);
        Assert.True(state.Dirty);
        Assert.Equal(1, changed);

        state.RenameInstrument(instrument, "Layer"); // same name: a no-op, like RenameTrack
        Assert.Equal(1, changed);
        state.RenameInstrument(instrument, "Drums");
        Assert.Equal("Drums", instrument.Name);
        Assert.Equal(2, changed);

        state.SetInstrumentSounds(instrument,
            [new InstrumentSound { Sound = "kick" }, new InstrumentSound { Sound = "snare" }]);
        Assert.Equal(["kick", "snare"], instrument.Sounds.Select(sound => sound.Sound));

        var track = state.AddTrack();
        var note = state.AddNote(track.Segments[0], 0, instrument, 0);
        Assert.False(state.RemoveInstrument(instrument)); // refused: still referenced

        state.RemoveNote(track.Segments[0], note);
        Assert.True(state.RemoveInstrument(instrument));
        Assert.Empty(state.Project.Instruments);
    }

    [Fact]
    public void RemoveInstrument_FallsBackTheActiveInstrument_WhenItWasTheOneRemoved()
    {
        var state = new EditorState();
        var instrument = state.AddInstrument("Layer");
        var other = state.AddInstrument("Other");
        state.ActiveInstrument = instrument;

        Assert.True(state.RemoveInstrument(instrument));
        Assert.Same(other, state.ActiveInstrument);
    }

    [Fact]
    public void DeleteInstrumentEverywhere_RemovesReferencingNotesThenTheInstrument()
    {
        var state = new EditorState();
        var instrument = state.AddInstrument("Layer");
        var other = state.AddInstrument("Other");
        var track = state.AddTrack();
        var note = state.AddNote(track.Segments[0], 0, instrument, 0);
        var otherNote = state.AddNote(track.Segments[0], 1, other, 0);

        state.DeleteInstrumentEverywhere(instrument);

        Assert.DoesNotContain(instrument, state.Project.Instruments);
        Assert.DoesNotContain(note, track.Segments[0].Notes);
        Assert.Contains(otherNote, track.Segments[0].Notes);
    }

    [Fact]
    public void DeleteInstrumentEverywhere_ClearsSelectedNote_WhenItUsedTheDeletedInstrument()
    {
        var state = new EditorState();
        var instrument = state.AddInstrument("Layer");
        var track = state.AddTrack();
        var note = state.AddNote(track.Segments[0], 0, instrument, 0);
        state.SelectNote(note);

        state.DeleteInstrumentEverywhere(instrument);

        Assert.Null(state.SelectedNote);
    }

    [Fact]
    public void NewProject_ReplacesEverything()
    {
        var state = new EditorState();
        state.SelectTrack(state.AddTrack());
        var changed = 0;
        state.OnProjectChanged += () => changed++;

        state.NewProject();

        Assert.Empty(state.Project.Tracks);
        Assert.Null(state.SelectedTrack);
        Assert.False(state.Dirty);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void SaveAndLoadProjectFile_RoundTrips_TracksThePath_AndClearsDirty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"editor-state-{Guid.NewGuid():N}.tdwproj");
        try
        {
            var state = new EditorState();
            state.RenameTrack(state.AddTrack(), "Drums");
            Assert.Null(state.ProjectPath);

            state.SaveProjectToFile(path);
            Assert.False(state.Dirty);
            Assert.Equal(path, state.ProjectPath);

            var loaded = new EditorState();
            loaded.LoadProjectFromFile(path);
            Assert.Equal(path, loaded.ProjectPath);
            Assert.Equal("Drums", loaded.Project.Tracks.Single().Name);
            Assert.False(loaded.Dirty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NewProject_ClearsTheProjectPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"editor-state-{Guid.NewGuid():N}.tdwproj");
        try
        {
            var state = new EditorState();
            state.SaveProjectToFile(path);

            state.NewProject();
            Assert.Null(state.ProjectPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Undo_AddNote_RemovesIt_AndRedo_RestoresIt()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");

        var note = state.AddNote(segment, 4, boom, -3);
        Assert.True(state.CanUndo);

        state.Undo();
        Assert.Empty(segment.Notes);
        Assert.False(state.CanUndo);
        Assert.True(state.CanRedo);

        state.Redo();
        Assert.Equal([note], segment.Notes);
        Assert.True(state.CanUndo);
        Assert.False(state.CanRedo);
    }

    [Fact]
    public void Undo_RemoveNote_RestoresTheSameInstance_WithItsFieldsIntact()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(segment, 4, boom, -3);
        note.Volume = 42;
        note.Pan = 10;

        Assert.True(state.RemoveNote(segment, note));
        Assert.Empty(segment.Notes);

        state.Undo();
        var restored = Assert.Single(segment.Notes);
        Assert.Same(note, restored);
        Assert.Equal(42, restored.Volume);
        Assert.Equal(10, restored.Pan);
    }

    [Fact]
    public void Undo_MoveNoteDrag_CollapsesTheWholeGesture_IntoOneStep()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(segment, 0, boom, 0);

        state.BeginGesture();
        state.MoveNote(segment, segment, note, 1, 0);
        state.MoveNote(segment, segment, note, 2, 0);
        state.MoveNote(segment, segment, note, 3, 0);

        state.Undo(); // undoes the whole drag, not just the last frame
        Assert.Equal(0, note.Step);

        state.Redo();
        Assert.Equal(3, note.Step); // jumps straight to the final dragged position
    }

    [Fact]
    public void MoveNote_ANewGesture_DoesNotMergeWithThePriorDrag()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(segment, 0, boom, 0);

        state.BeginGesture();
        state.MoveNote(segment, segment, note, 1, 0);

        state.BeginGesture(); // a second, separate drag on the same note
        state.MoveNote(segment, segment, note, 2, 0);

        state.Undo();
        Assert.Equal(1, note.Step); // only the second drag undone
        state.Undo();
        Assert.Equal(0, note.Step); // then the first
    }

    [Fact]
    public void MoveSelectedNotes_MovesEveryNoteTogether_AsOneUndoEntry()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(segment, 0, boom, 0);
        var b = state.AddNote(segment, 5, boom, 3);

        state.BeginGesture();
        state.MoveSelectedNotes(track, [(a, segment, 1, 1), (b, segment, 6, 4)]);
        state.MoveSelectedNotes(track, [(a, segment, 2, 2), (b, segment, 7, 5)]);

        Assert.Equal(2, a.Step);
        Assert.Equal(2, a.Value);
        Assert.Equal(7, b.Step);
        Assert.Equal(5, b.Value);

        state.Undo(); // one entry reverts BOTH notes to their pre-drag state
        Assert.Equal(0, a.Step);
        Assert.Equal(0, a.Value);
        Assert.Equal(5, b.Step);
        Assert.Equal(3, b.Value);

        state.Redo();
        Assert.Equal(2, a.Step);
        Assert.Equal(7, b.Step);
    }

    [Fact]
    public void MoveSelectedNotes_ANewGesture_DoesNotMergeWithThePriorDrag()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(segment, 0, boom, 0);

        state.BeginGesture();
        state.MoveSelectedNotes(track, [(note, segment, 1, 0)]);

        state.BeginGesture(); // a second, separate drag
        state.MoveSelectedNotes(track, [(note, segment, 2, 0)]);

        state.Undo();
        Assert.Equal(1, note.Step); // only the second drag undone
        state.Undo();
        Assert.Equal(0, note.Step); // then the first
    }

    [Fact]
    public void MoveSelectedNotes_MovesNotesAcrossSegments()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var first = track.Segments[0];
        var second = state.AddSegment(track);
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(first, 3, boom, 0);

        state.MoveSelectedNotes(track, [(note, second, 0, 0)]);

        Assert.Empty(first.Notes);
        Assert.Equal([note], second.Notes);

        state.Undo();
        Assert.Equal([note], first.Notes);
        Assert.Empty(second.Notes);
    }

    [Fact]
    public void MoveSelectedNotes_NoActualChange_DoesNotDirtyOrPushUndo()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(segment, 3, boom, 0);
        state.SaveProject(); // clears dirty

        state.MoveSelectedNotes(track, [(note, segment, 3, 0)]); // identical position

        Assert.False(state.Dirty); // Touch() never ran: proves the no-op guard short-circuited
    }

    [Fact]
    public void Undo_MovePlacementDrag_CollapsesTheWholeGesture_IntoOneStep()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 0, 0);

        state.BeginGesture();
        state.MovePlacement(placement, 1, 4);
        state.MovePlacement(placement, 2, 8);

        state.Undo();
        Assert.Equal(0, placement.Channel);
        Assert.Equal(0, placement.StartQuarterNotes);

        state.Redo();
        Assert.Equal(2, placement.Channel);
        Assert.Equal(8, placement.StartQuarterNotes);
    }

    [Fact]
    public void Undo_RemovePlacement_RestoresTheSameInstance()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 2, 8);

        Assert.True(state.RemovePlacement(placement));
        Assert.Empty(state.Project.Placements);

        state.Undo();
        Assert.Same(placement, Assert.Single(state.Project.Placements));
    }

    [Fact]
    public void Undo_RemoveTrack_RestoresTheSameInstance_AtItsOriginalIndex()
    {
        var state = new EditorState();
        var first = state.AddTrack();
        var track = state.AddTrack();
        var third = state.AddTrack();

        Assert.True(state.RemoveTrack(track));
        Assert.Equal([first, third], state.Project.Tracks);

        state.Undo();
        Assert.Equal([first, track, third], state.Project.Tracks);

        state.Redo();
        Assert.Equal([first, third], state.Project.Tracks);
    }

    [Fact]
    public void Undo_RemoveTrack_RestoresItsCascadedPlacements()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 0, 0);

        Assert.True(state.RemoveTrack(track));
        Assert.Empty(state.Project.Placements);

        state.Undo();
        Assert.Same(placement, Assert.Single(state.Project.Placements));
    }

    [Fact]
    public void Undo_RemoveSegment_RestoresTheSameInstance_AtItsOriginalIndex()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var first = track.Segments[0];
        var segment = state.AddSegment(track);
        var third = state.AddSegment(track);

        Assert.True(state.RemoveSegment(track, segment));
        Assert.Equal([first, third], track.Segments);

        state.Undo();
        Assert.Equal([first, segment, third], track.Segments);

        state.Redo();
        Assert.Equal([first, third], track.Segments);
    }

    [Fact]
    public void Undo_DeleteInstrumentEverywhere_RestoresTheInstrumentAndEveryRemovedNote()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var kept = MakeInstrument(state, "kept");
        var note = state.AddNote(segment, 0, boom, 0);
        var other = state.AddNote(segment, 1, kept, 0);

        state.DeleteInstrumentEverywhere(boom);
        Assert.DoesNotContain(boom, state.Project.Instruments);
        Assert.Equal([other], segment.Notes);

        state.Undo();
        Assert.Contains(boom, state.Project.Instruments);
        Assert.Equal([note, other], segment.Notes);

        state.Redo();
        Assert.DoesNotContain(boom, state.Project.Instruments);
        Assert.Equal([other], segment.Notes);
    }

    [Fact]
    public void Undo_PlaceTrack_RemovesIt_AndRedoRestoresTheSameInstance()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 2, 8);

        state.Undo();
        Assert.Empty(state.Project.Placements);

        state.Redo();
        Assert.Same(placement, Assert.Single(state.Project.Placements));
    }

    [Fact]
    public void NewAction_AfterUndo_ClearsTheRedoStack()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");

        state.AddNote(segment, 0, boom, 0);
        state.Undo();
        Assert.True(state.CanRedo);

        state.AddNote(segment, 1, boom, 0);
        Assert.False(state.CanRedo);
    }

    [Fact]
    public void NewProject_ClearsUndoHistory()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        state.PlaceTrack(track, 0, 0);
        Assert.True(state.CanUndo);

        state.NewProject();
        Assert.False(state.CanUndo);
        Assert.False(state.CanRedo);
    }

    [Fact]
    public void LoadProject_ClearsUndoHistory()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        state.PlaceTrack(track, 0, 0);
        var json = state.SaveProject();
        Assert.True(state.CanUndo);

        state.LoadProject(json);
        Assert.False(state.CanUndo);
        Assert.False(state.CanRedo);
    }

    [Fact]
    public void Undo_WithEmptyStack_DoesNothing()
    {
        var state = new EditorState();
        state.Undo();
        state.Redo();
        Assert.False(state.CanUndo);
        Assert.False(state.CanRedo);
    }

    [Fact]
    public void SelectingANote_CopiesItsModifiers_AndTheNextPlacementInheritsThem()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var source = state.AddNote(segment, 0, boom, 5);
        source.Volume = 42;
        source.Pan = -10;
        source.Offset = 0.5;
        var automation = new AudioKeyframeManager();
        source.Automation = automation;

        state.SelectNote(source);

        var placed = state.AddNote(segment, 4, boom, 99); // a different pitch value
        Assert.Equal(99, placed.Value); // value is never copied
        Assert.Equal(42, placed.Volume);
        Assert.Equal(-10, placed.Pan);
        Assert.Equal(0.5, placed.Offset);
        Assert.NotSame(automation,
            placed.Automation); // cloned: editing one note's automation must not affect the other
        Assert.NotNull(placed.Automation);
    }

    [Fact]
    public void ClosingTheNoteEditor_ClearsTheCopiedModifiers()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var source = state.AddNote(segment, 0, boom, 0);
        source.Volume = 42;

        state.OpenTrack(track);
        state.SelectNote(source);
        Assert.Same(source, state.CopiedModifiers);

        state.CloseTrack();
        Assert.Null(state.CopiedModifiers);

        var placed = state.AddNote(segment, 1, boom, 0);
        Assert.Null(placed.Volume);
    }

    [Fact]
    public void WithoutASelectedNote_NewNotesGetNoModifiers()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");

        var note = state.AddNote(segment, 0, boom, 0);
        Assert.Null(note.Volume);
        Assert.Equal(0, note.Pan);
        Assert.Equal(0, note.Offset);
        Assert.Null(note.Automation);
    }

    [Fact]
    public void ActiveTool_DefaultsToDraw_AndFiresOnlyOnChange()
    {
        var state = new EditorState();
        Assert.Equal(EditorTool.Draw, state.ActiveTool);

        var fired = 0;
        state.OnToolChanged += _ => fired++;

        state.ActiveTool = EditorTool.Select;
        state.ActiveTool = EditorTool.Select; // no-op: same value
        Assert.Equal(1, fired);

        state.ActiveTool = EditorTool.Draw;
        Assert.Equal(2, fired);
    }

    [Fact]
    public void SetNoteSelection_ReplacesTheList_AndFiresOncePerBatch()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(segment, 0, boom, 0);
        var b = state.AddNote(segment, 1, boom, 0);
        var c = state.AddNote(segment, 2, boom, 0);

        var fired = 0;
        state.OnNoteSelectionChanged += _ => fired++;

        state.SetNoteSelection([a, b]);
        Assert.Equal([a, b], state.SelectedNotes);
        Assert.Null(state.SelectedNote); // derived view: only non-null for exactly one
        Assert.Equal(1, fired);

        state.SetNoteSelection([a, b]); // same content: no-op, no event
        Assert.Equal(1, fired);

        state.SetNoteSelection([c]);
        Assert.Equal(c, state.SelectedNote);
        Assert.Equal(2, fired);
    }

    [Fact]
    public void AddAndRemoveFromNoteSelection_AreAppendAndRemoveSemantics()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var a = state.AddNote(segment, 0, boom, 0);
        var b = state.AddNote(segment, 1, boom, 0);

        state.SetNoteSelection([a]);
        var fired = 0;
        state.OnNoteSelectionChanged += _ => fired++;

        state.AddToNoteSelection([a]); // already present: no-op
        Assert.Equal(0, fired);

        state.AddToNoteSelection([b]);
        Assert.Equal([a, b], state.SelectedNotes);
        Assert.Equal(1, fired);

        state.RemoveFromNoteSelection([a]);
        Assert.Equal([b], state.SelectedNotes);
        Assert.Equal(2, fired);

        state.RemoveFromNoteSelection([a]); // no longer present: no-op
        Assert.Equal(2, fired);
    }

    [Fact]
    public void SelectAll_SelectsEveryNoteOfTheOpenedTrack_OrElseEveryPlacement()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        var second = state.AddSegment(track);
        var a = state.AddNote(track.Segments[0], 0, boom, 0);
        var b = state.AddNote(second, 0, boom, 0);

        state.OpenTrack(track);
        state.SelectAll();
        Assert.Equal([a, b], state.SelectedNotes);

        state.CloseTrack();
        var p1 = state.PlaceTrack(track, 0, 0);
        var p2 = state.PlaceTrack(track, 1, 4);
        state.SelectAll();
        Assert.Equal([p1, p2], state.SelectedPlacements);
    }

    [Fact]
    public void ClearSelection_ClearsBothLists()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var note = state.AddNote(track.Segments[0], 0, MakeInstrument(state, "boom"), 0);
        state.OpenTrack(track);
        state.SetNoteSelection([note]);

        state.ClearSelection();
        Assert.Empty(state.SelectedNotes);
        Assert.Empty(state.SelectedPlacements);
    }

    [Fact]
    public void RemoveSegment_PrunesEveryNoteOfThatSegment_FromAMultiSelection()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        var doomed = state.AddSegment(track);
        var kept = state.AddNote(track.Segments[0], 0, boom, 0);
        var removed = state.AddNote(doomed, 0, boom, 0);
        state.SetNoteSelection([kept, removed]);

        Assert.True(state.RemoveSegment(track, doomed));

        Assert.Equal([kept], state.SelectedNotes);
    }

    [Fact]
    public void RemoveTrack_PrunesItsCascadedPlacements_FromAMultiSelection()
    {
        var state = new EditorState();
        var doomed = state.AddTrack();
        var kept = state.AddTrack();
        var p1 = state.PlaceTrack(doomed, 0, 0);
        var p2 = state.PlaceTrack(kept, 1, 0);
        state.SetPlacementSelection([p1, p2]);

        Assert.True(state.RemoveTrack(doomed));

        Assert.Equal([p2], state.SelectedPlacements);
    }

    [Fact]
    public void DeleteInstrumentEverywhere_PrunesEveryAffectedNote_FromAMultiSelection()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var doomed = MakeInstrument(state, "boom");
        var kept = MakeInstrument(state, "kept");
        var a = state.AddNote(track.Segments[0], 0, doomed, 0);
        var b = state.AddNote(track.Segments[0], 1, kept, 0);
        state.SetNoteSelection([a, b]);

        state.DeleteInstrumentEverywhere(doomed);

        Assert.Equal([b], state.SelectedNotes);
    }

    [Fact]
    public void DeleteSelection_RemovesEveryNote_AsOneUndoEntry()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        var segment = track.Segments[0];
        var a = state.AddNote(segment, 0, boom, 0);
        var b = state.AddNote(segment, 1, boom, 0);
        var c = state.AddNote(segment, 2, boom, 0);
        state.OpenTrack(track);
        state.SetNoteSelection([a, b]);

        state.DeleteSelection();

        Assert.Equal([c], segment.Notes);
        Assert.Empty(state.SelectedNotes);

        state.Undo(); // one Ctrl+Z restores both
        Assert.Equal([c, a, b], segment.Notes);

        state.Redo();
        Assert.Equal([c], segment.Notes);
    }

    [Fact]
    public void DeleteSelection_RemovesEveryPlacement_AsOneUndoEntry()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var p1 = state.PlaceTrack(track, 0, 0);
        var p2 = state.PlaceTrack(track, 1, 4);
        var p3 = state.PlaceTrack(track, 2, 8);
        state.SetPlacementSelection([p1, p2]);

        state.DeleteSelection();

        Assert.Equal([p3], state.Project.Placements);

        state.Undo();
        Assert.Equal([p3, p1, p2], state.Project.Placements);
    }

    [Fact]
    public void CopyAndPasteNotes_IntoADifferentTrack_PastesInPlace_AndBecomesTheSelection()
    {
        var state = new EditorState();
        var source = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(source.Segments[0], 3, boom, 5);
        note.Volume = 42;
        state.OpenTrack(source);
        state.SetNoteSelection([note]);
        state.CopySelection();
        state.CloseTrack();

        var target = state.AddTrack();
        state.OpenTrack(target);
        state.Paste();

        var pasted = Assert.Single(target.Segments[0].Notes);
        Assert.Equal(3, pasted.Step);
        Assert.Equal(5, pasted.Value);
        Assert.Equal(42, pasted.Volume);
        Assert.NotSame(note, pasted);
        Assert.Equal([pasted], state.SelectedNotes); // the paste becomes the new selection
    }

    [Fact]
    public void PasteInPlace_OntoTheSameTrack_StacksOnTheOriginal()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        var segment = track.Segments[0];
        var note = state.AddNote(segment, 0, boom, 0);
        state.OpenTrack(track);
        state.SetNoteSelection([note]);
        state.CopySelection();

        state.Paste(); // same (Step, Value) as the still-present original: stacked, not nudged right
        Assert.Equal(2, segment.Notes.Count);
        Assert.All(segment.Notes, n => Assert.Equal(0, n.Step));

        state.Paste();
        Assert.Equal(3, segment.Notes.Count);
        Assert.All(segment.Notes, n => Assert.Equal(0, n.Step));
    }

    [Fact]
    public void PasteInPlace_OfAFullBar_ClonesEveryNoteOntoItsOwnStep()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        var segment = track.Segments[0];
        segment.StepsPerBeat = 1; // 4/4, one step per beat: steps 0-3 are the bar's four beats
        var beats = Enumerable.Range(0, 4).Select(step => state.AddNote(segment, step, boom, 0)).ToArray();
        state.OpenTrack(track);
        state.SetNoteSelection(beats);
        state.CopySelection();

        state.Paste();

        // One-to-one with the copy: four clones on the four occupied beats, nothing dropped
        // and nothing shifted a beat right.
        Assert.Equal(8, segment.Notes.Count);
        Assert.Equal([0, 0, 1, 1, 2, 2, 3, 3], segment.Notes.Select(n => n.Step).Order());
        Assert.Equal(4, state.SelectedNotes.Count);
        Assert.Empty(state.SelectedNotes.Intersect(beats));

        // The pasted block is the selection, so it can be dragged off the originals whole.
        state.MoveSelectedNotes(track,
            [.. state.SelectedNotes.Select(n => (n, segment, n.Step + 4, n.Value))]);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7], segment.Notes.Select(n => n.Step).Order());
    }

    [Fact]
    public void PasteNotes_DropsThoseBeyondTheTargetTracksEnd()
    {
        var state = new EditorState();
        var source = state.AddTrack(); // default: one 16-step segment
        var boom = MakeInstrument(state, "boom");
        var fits = state.AddNote(source.Segments[0], 0, boom, 0);
        var beyond = state.AddNote(source.Segments[0], 15, boom, 0);
        state.OpenTrack(source);
        state.SetNoteSelection([fits, beyond]);
        state.CopySelection();
        state.CloseTrack();

        var target = state.AddTrack();
        target.Segments[0].Bars = 1;
        target.Segments[0].Numerator = 1;
        target.Segments[0].StepsPerBeat = 4; // 4 steps total: global step 15 falls off the end
        state.OpenTrack(target);

        state.Paste();

        var pasted = Assert.Single(target.Segments[0].Notes);
        Assert.Equal(0, pasted.Step); // only the in-range note survived, not truncated to step 3
    }

    [Fact]
    public void CopyPasteNotes_TwiceInDifferentTracks_ProduceIndependentClones()
    {
        var state = new EditorState();
        var source = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        var note = state.AddNote(source.Segments[0], 0, boom, 3);
        note.Automation = new AudioKeyframeManager();
        state.OpenTrack(source);
        state.SetNoteSelection([note]);
        state.CopySelection();
        state.CloseTrack();

        var trackA = state.AddTrack();
        var trackB = state.AddTrack();
        state.OpenTrack(trackA);
        state.Paste();
        var pastedA = Assert.Single(trackA.Segments[0].Notes);
        state.CloseTrack();

        state.OpenTrack(trackB);
        state.Paste();
        var pastedB = Assert.Single(trackB.Segments[0].Notes);

        Assert.NotSame(pastedA, pastedB);
        Assert.NotSame(pastedA.Automation, pastedB.Automation);

        // Editing one clone's automation must not reach the other.
        pastedA.Automation!.Repeats = 5;
        Assert.Equal(1, pastedB.Automation!.Repeats);
    }

    [Fact]
    public void Paste_CrossEditorMismatch_IsANoOp()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var placement = state.PlaceTrack(track, 0, 0);
        state.SetPlacementSelection([placement]);
        state.CopySelection(); // clipboard now holds a placement payload

        state.OpenTrack(track); // arrangement no longer shown
        state.Paste();

        Assert.Empty(track.Segments[0].Notes); // placements payload while a track is open: no-op
    }

    [Fact]
    public void CutSelection_CopiesThenDeletes_AsOneUndoEntry()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var boom = MakeInstrument(state, "boom");
        var segment = track.Segments[0];
        var note = state.AddNote(segment, 0, boom, 0);
        state.OpenTrack(track);
        state.SetNoteSelection([note]);

        state.CutSelection();
        Assert.Empty(segment.Notes);

        state.Paste();
        Assert.Single(segment.Notes); // the cut note round-trips through the clipboard

        state.Undo(); // undoes the paste, not the cut (cut = copy + one delete undo entry)
        Assert.Empty(segment.Notes);
    }

    [Fact]
    public void Replace_ClearsTheClipboard()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var note = state.AddNote(track.Segments[0], 0, MakeInstrument(state, "boom"), 0);
        state.OpenTrack(track);
        state.SetNoteSelection([note]);
        state.CopySelection();

        state.NewProject();

        var freshTrack = state.AddTrack();
        state.OpenTrack(freshTrack);
        state.Paste(); // stale clipboard referencing the dead project must not resurrect anything
        Assert.Empty(freshTrack.Segments[0].Notes);
    }

    [Fact]
    public void RemoveTrack_DropsClipboardEntries_ForTheRemovedTrackOnly()
    {
        var state = new EditorState();
        var doomed = state.AddTrack();
        var kept = state.AddTrack();
        var p1 = state.PlaceTrack(doomed, 0, 0);
        var p2 = state.PlaceTrack(kept, 1, 0);
        state.SetPlacementSelection([p1, p2]);
        state.CopySelection();

        state.RemoveTrack(doomed); // cascades p1 away; clipboard entry for `doomed` must drop too
        state.Paste();

        // Only kept's surviving clipboard entry pastes back - one new clone, not two.
        Assert.Equal([p2, state.SelectedPlacement!], state.Project.Placements);
        Assert.Equal(kept, state.SelectedPlacement!.Track);
    }

    [Fact]
    public void ImportSequenceAsTrack_IsOneUndoStep_RemovingTrackInstrumentsAndPlacementTogether()
    {
        var state = new EditorState();
        var sequence = Sequence.FromString("kick|snare");

        var result = state.ImportSequenceAsTrack(sequence, "imported", null);

        Assert.Single(state.Project.Tracks);
        Assert.Equal(2, state.Project.Instruments.Count);
        Assert.Single(state.Project.Placements);
        Assert.True(state.Dirty);

        state.Undo();
        Assert.Empty(state.Project.Tracks);
        Assert.Empty(state.Project.Instruments);
        Assert.Empty(state.Project.Placements);

        state.Redo();
        Assert.Equal([result.Track!], state.Project.Tracks);
        Assert.Equal([result.Placement!], state.Project.Placements);
        Assert.Equal(2, state.Project.Instruments.Count);
    }

    [Fact]
    public void ImportSequenceAsTrack_PlacesOnTheNextFreeChannel()
    {
        var state = new EditorState();
        state.PlaceTrack(state.AddTrack(), 3, 0);

        var result = state.ImportSequenceAsTrack(Sequence.FromString("kick"), "imported", null);

        Assert.Equal(4, result.Placement!.Channel);
    }

    [Fact]
    public void ReplaceWithImportedProject_ClearsUndo_SetsDirty_AndNullsProjectPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"editor-state-{Guid.NewGuid():N}.tdwproj");
        try
        {
            var state = new EditorState();
            state.AddTrack();
            state.SaveProjectToFile(path);
            Assert.False(state.Dirty);

            state.ReplaceWithImportedProject(Sequence.FromString("kick|snare"), "song", null);

            Assert.Equal(2, state.Project.Tracks.Count); // one per distinct sound
            Assert.True(state.Dirty); // exists only in memory - the exit guard must still fire
            Assert.Null(state.ProjectPath); // so Save asks for a location, not overwrite the old file
            Assert.False(state.CanUndo); // import-as-project isn't undoable
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddNote_AsACut_IgnoresCopiedModifiers()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        var kick = MakeInstrument(state, "kick");
        var source = state.AddNote(segment, 0, boom, 5);
        source.Volume = 42;
        source.Pan = -10;
        source.Offset = 0.5;
        source.Automation = new AudioKeyframeManager();
        state.SelectNote(source); // seeds CopiedModifiers

        var cutNote = state.AddNote(segment, 1, kick, 0, true);

        Assert.True(cutNote.IsCut);
        Assert.Null(cutNote.Volume);
        Assert.Equal(0, cutNote.Pan);
        Assert.Equal(0, cutNote.Offset);
        Assert.Null(cutNote.Automation);
    }
}
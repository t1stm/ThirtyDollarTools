using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

public class EditorStateTests
{
    private static Instrument MakeInstrument(EditorState state, string sound)
    {
        var instrument = state.AddInstrument(sound);
        state.SetInstrumentSounds(instrument, [sound]);
        return instrument;
    }

    [Fact]
    public void AddTrack_MutatesProject_SetsDirty_AndNotifies()
    {
        var state = new EditorState();
        var changed = 0;
        state.OnProjectChanged = () => changed++;

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

        ProjectTrack? selected = track;
        state.OnSelectionChanged = t => selected = t;

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
        state.OnProjectChanged = () => fired++;

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
        state.OnProjectChanged = () => fired++;
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
        state.OnSelectionChanged = _ => fired++;

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
        state.OnProjectChanged = () => changed++;

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
        state.OnChannelsChanged = () => fired++;

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
        state.OnOpenedTrackChanged = t => opened = t;

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
    public void NoteLifecycle_MutatesDirtiesAndNotifies()
    {
        var state = new EditorState();
        var track = state.AddTrack();
        var segment = track.Segments[0];
        var boom = MakeInstrument(state, "boom");
        state.SaveProject(); // clears dirty

        var changed = 0;
        state.OnProjectChanged = () => changed++;

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
        state.OnInstrumentsChanged = () => changed++;

        var instrument = state.AddInstrument("Layer");
        Assert.Equal([instrument], state.Project.Instruments);
        Assert.True(state.Dirty);
        Assert.Equal(1, changed);

        state.RenameInstrument(instrument, "Layer"); // same name: a no-op, like RenameTrack
        Assert.Equal(1, changed);
        state.RenameInstrument(instrument, "Drums");
        Assert.Equal("Drums", instrument.Name);
        Assert.Equal(2, changed);

        state.SetInstrumentSounds(instrument, ["kick", "snare"]);
        Assert.Equal(["kick", "snare"], instrument.Sounds);

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
        state.OnProjectChanged = () => changed++;

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
        Assert.Same(automation, placed.Automation);
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
}

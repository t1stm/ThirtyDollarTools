using EditorScene.Scenes.Components;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Tests;

// Headless: fields are reached through InspectorPanel.Field("Section.Label") and
// driven by setting their values, which fires the same change events the pointer
// and keyboard paths do.
public class InspectorPanelTests
{
    private static (EditorTestContext ctx, EditorState state, InspectorPanel inspector) NewInspector()
    {
        var ctx = new EditorTestContext();
        var state = new EditorState();
        var inspector = new InspectorPanel(ctx, state) { Width = 260, Height = 600 };

        // The EditorInterface wiring.
        state.OnProjectChanged = inspector.Sync;
        state.OnSelectionChanged = _ => inspector.Rebuild();
        state.OnOpenedTrackChanged = _ => inspector.Rebuild();
        state.OnSegmentSelectionChanged = _ => inspector.Rebuild();
        state.OnNoteSelectionChanged = _ => inspector.Rebuild();

        inspector.Layout();
        return (ctx, state, inspector);
    }

    [Fact]
    public void ArrangementMode_EditsProjectFields_ThroughState()
    {
        var (_, state, inspector) = NewInspector();

        ((TextInput)inspector.Field("Project.Name")!).Value = "My Song";
        ((TextInput)inspector.Field("Project.Author")!).Value = "Kris";
        ((NumericInput)inspector.Field("Project.BPM")!).Value = 200;

        Assert.Equal("My Song", state.Project.Info.Name);
        Assert.Equal("Kris", state.Project.Info.Author);
        Assert.Equal(200, state.Project.RootTiming.BPM);
        Assert.True(state.Dirty);

        // No track selected, no note editor: only the project section exists.
        Assert.Null(inspector.Field("Track.Name"));
        Assert.Null(inspector.Field("Segment.Bars"));
    }

    [Fact]
    public void SelectingATrack_ShowsItsSection_AndTheTempoCheckboxSwapsTiming()
    {
        var (_, state, inspector) = NewInspector();
        var track = state.AddTrack();
        state.SelectTrack(track);

        ((TextInput)inspector.Field("Track.Name")!).Value = "Drums";
        Assert.Equal("Drums", track.Name);

        // Following the project tempo: no own-BPM row.
        Assert.Null(inspector.Field("Track.BPM"));

        ((Checkbox)inspector.Field("Track.Project tempo")!).Checked = false;
        Assert.False(state.TrackFollowsRootTiming(track));

        var bpm = (NumericInput)inspector.Field("Track.BPM")!;
        bpm.Value = 90;
        Assert.Equal(90, track.Timing.BPM);
        Assert.NotEqual(90, state.Project.RootTiming.BPM);

        ((Checkbox)inspector.Field("Track.Project tempo")!).Checked = true;
        Assert.True(state.TrackFollowsRootTiming(track));
        Assert.Null(inspector.Field("Track.BPM"));
    }

    [Fact]
    public void NoteEditorMode_EditsSegmentAndNoteFields()
    {
        var (_, state, inspector) = NewInspector();
        var track = state.AddTrack();
        state.OpenTrack(track);
        var segment = track.Segments[0];
        var note = state.AddNote(segment, 2, "boom", 0);
        state.SelectNote(note);

        ((NumericInput)inspector.Field("Segment.Bars")!).Value = 2;
        Assert.Equal(2, segment.Bars);

        // Fractional int fields round; the min clamps garbage.
        ((NumericInput)inspector.Field("Segment.Steps/beat")!).Value = 2.6;
        Assert.Equal(3, segment.StepsPerBeat);

        // The value range is the TDW default ±60, not the old ±24.
        ((NumericInput)inspector.Field("Note.Value")!).Value = -48;
        ((NumericInput)inspector.Field("Note.Pan")!).Value = 50;
        ((NumericInput)inspector.Field("Note.Offset (s)")!).Value = 0.25;
        Assert.Equal(-48, note.Value);
        Assert.Equal(50, note.Pan);
        Assert.Equal(0.25, note.Offset);

        // The arrangement sections are gone while the editor is open.
        Assert.Null(inspector.Field("Project.Name"));
    }

    [Fact]
    public void NullableFields_CommitEmptyAsInherit()
    {
        var (_, state, inspector) = NewInspector();
        var track = state.AddTrack();
        state.OpenTrack(track);
        var segment = track.Segments[0];
        var note = state.AddNote(segment, 0, "boom", 0);
        state.SelectNote(note);

        var segmentBpm = (NumericInput)inspector.Field("Segment.BPM")!;
        segmentBpm.Value = 180;
        Assert.Equal(180f, segment.BPM);
        segmentBpm.Value = null;
        Assert.Null(segment.BPM);

        var volume = (NumericInput)inspector.Field("Note.Volume")!;
        volume.Value = 80;
        Assert.Equal(80d, note.Volume);
        volume.Value = null;
        Assert.Null(note.Volume);
    }

    [Fact]
    public void Sync_RefreshesUnfocusedFields_ButNeverTheOneBeingTyped()
    {
        var (ctx, state, inspector) = NewInspector();
        var track = state.AddTrack();
        state.OpenTrack(track);
        var note = state.AddNote(track.Segments[0], 0, "boom", 0);
        state.SelectNote(note);

        // A drag in the note editor moves the note; the inspector follows.
        state.MoveNote(track.Segments[0], track.Segments[0], note, 1, 5);
        Assert.Equal(5, ((NumericInput)inspector.Field("Note.Value")!).Value);

        // While the value field is being typed in, sync leaves it alone.
        var valueField = (NumericInput)inspector.Field("Note.Value")!;
        ctx.Focus(valueField);
        state.MoveNote(track.Segments[0], track.Segments[0], note, 1, 7);
        Assert.Equal(5, valueField.Value);

        ctx.Blur();
        inspector.Sync();
        Assert.Equal(7, valueField.Value);
    }

    [Fact]
    public void AutomationSection_BuildsAndEditsKeyframes_ThroughState()
    {
        var (_, state, inspector) = NewInspector();
        var track = state.AddTrack();
        state.OpenTrack(track);
        var note = state.AddNote(track.Segments[0], 0, "boom", 0);
        state.SelectNote(note);

        // No automation yet: only the add button exists.
        Assert.NotNull(inspector.Field("Automation.+ Add automation"));
        Assert.Null(inspector.Field("Automation.+ Keyframe"));

        ((Button)inspector.Field("Automation.+ Add automation")!).OnClick!.Invoke(null!);
        Assert.NotNull(note.Automation);

        ((Button)inspector.Field("Automation.+ Keyframe")!).OnClick!.Invoke(null!);
        var keyframe = Assert.Single(note.Automation!.Keyframes);

        ((NumericInput)inspector.Field("Keyframe 1.Gap")!).Value = 2.5;
        Assert.Equal(2.5f, keyframe.Gap);

        // Amount + the "×" checkbox commit as one modifier.
        ((NumericInput)inspector.Field("Keyframe 1.Value")!).Value = 3;
        Assert.Equal(new Modifier(3), keyframe.Value);
        ((Checkbox)inspector.Field("Keyframe 1.Value.Kind")!).Checked = true;
        Assert.Equal(new Modifier(3, ModifierKind.Multiply), keyframe.Value);

        ((NumericInput)inspector.Field("Keyframe 1.Offset")!).Value = 0.5;
        Assert.Equal(new Modifier(0.5), keyframe.Offset);

        ((Checkbox)inspector.Field("Automation.Gaps in seconds")!).Checked = true;
        Assert.Equal(KeyframeTiming.Time, note.Automation.Timing);

        ((NumericInput)inspector.Field("Automation.Repeats")!).Value = 4;
        Assert.Equal(4, note.Automation.Repeats);
        Assert.True(state.Dirty);

        ((Button)inspector.Field("Keyframe 1.Remove")!).OnClick!.Invoke(null!);
        Assert.Empty(note.Automation.Keyframes);

        ((Button)inspector.Field("Automation.Remove automation")!).OnClick!.Invoke(null!);
        Assert.Null(note.Automation);
        Assert.NotNull(inspector.Field("Automation.+ Add automation"));
    }

    [Fact]
    public void AutomationSection_SurvivesASaveLoadRoundTrip()
    {
        var (_, state, inspector) = NewInspector();
        var track = state.AddTrack();
        state.OpenTrack(track);
        var note = state.AddNote(track.Segments[0], 0, "boom", 0);
        note.Automation = new AudioKeyframeManager
        {
            Timing = KeyframeTiming.Time,
            Keyframes = { new AudioKeyframe { Gap = 1.5f, Value = new Modifier(2, ModifierKind.Multiply) } }
        };
        var json = state.SaveProject();

        state.LoadProject(json);
        var loadedTrack = state.Project.Tracks.Single();
        state.OpenTrack(loadedTrack);
        var loadedNote = loadedTrack.Segments[0].Notes.Single();
        state.SelectNote(loadedNote);

        Assert.Equal(1.5, ((NumericInput)inspector.Field("Keyframe 1.Gap")!).Value);
        Assert.True(((Checkbox)inspector.Field("Keyframe 1.Value.Kind")!).Checked);
        Assert.True(((Checkbox)inspector.Field("Automation.Gaps in seconds")!).Checked);
    }

    [Fact]
    public void TrackAutomationSection_BuildsAndEditsKeyframes_ThroughState()
    {
        var (_, state, inspector) = NewInspector();
        var track = state.AddTrack();
        state.SelectTrack(track);

        // No automations yet: only the add button exists.
        Assert.NotNull(inspector.Field("Track Automation.+ Add automation"));
        Assert.Null(inspector.Field("Track Automation 1.+ Keyframe"));

        ((Button)inspector.Field("Track Automation.+ Add automation")!).OnClick!.Invoke(null!);
        var automation = Assert.Single(track.TrackAutomations);

        // Defaults to "all sounds" — no sound-count button yet.
        Assert.True(((Checkbox)inspector.Field("Track Automation 1.All sounds")!).Checked);
        Assert.Null(inspector.Field("Track Automation 1.Sounds"));

        ((Button)inspector.Field("Track Automation 1.+ Keyframe")!).OnClick!.Invoke(null!);
        var keyframe = Assert.Single(automation.Keyframes.Keyframes);

        ((NumericInput)inspector.Field("Track Automation 1 Keyframe 1.Gap")!).Value = 2.5;
        Assert.Equal(2.5f, keyframe.Gap);

        ((NumericInput)inspector.Field("Track Automation 1 Keyframe 1.Value")!).Value = 3;
        Assert.Equal(new Modifier(3), keyframe.Value);

        ((NumericInput)inspector.Field("Track Automation 1.Repeats")!).Value = 4;
        Assert.Equal(4, automation.Keyframes.Repeats);
        Assert.True(state.Dirty);

        ((Button)inspector.Field("Track Automation 1 Keyframe 1.Remove")!).OnClick!.Invoke(null!);
        Assert.Empty(automation.Keyframes.Keyframes);

        ((Button)inspector.Field("Track Automation 1.Remove")!).OnClick!.Invoke(null!);
        Assert.Empty(track.TrackAutomations);
        Assert.NotNull(inspector.Field("Track Automation.+ Add automation"));
    }

    [Fact]
    public void TrackAutomationSection_AllSoundsToggle_AndSoundPickerSeam()
    {
        var (_, state, inspector) = NewInspector();
        var track = state.AddTrack();
        state.SelectTrack(track);
        ((Button)inspector.Field("Track Automation.+ Add automation")!).OnClick!.Invoke(null!);
        var automation = Assert.Single(track.TrackAutomations);

        ((Checkbox)inspector.Field("Track Automation 1.All sounds")!).Checked = false;
        Assert.NotNull(automation.Sounds);
        Assert.Empty(automation.Sounds);
        Assert.NotNull(inspector.Field("Track Automation 1.Sounds"));

        TrackAutomation? seen = null;
        inspector.OnEditTrackAutomationSounds = a => seen = a;
        ((Button)inspector.Field("Track Automation 1.Sounds")!).OnClick!.Invoke(null!);
        Assert.Same(automation, seen);

        ((Checkbox)inspector.Field("Track Automation 1.All sounds")!).Checked = true;
        Assert.Null(automation.Sounds);
        Assert.Null(inspector.Field("Track Automation 1.Sounds"));
    }

    [Fact]
    public void TrackAutomationSection_MultipleEntries_HaveIndependentFieldKeys()
    {
        var (_, state, inspector) = NewInspector();
        var track = state.AddTrack();
        state.SelectTrack(track);
        ((Button)inspector.Field("Track Automation.+ Add automation")!).OnClick!.Invoke(null!);
        ((Button)inspector.Field("Track Automation.+ Add automation")!).OnClick!.Invoke(null!);
        var first = track.TrackAutomations[0];
        var second = track.TrackAutomations[1];

        ((Button)inspector.Field("Track Automation 1.+ Keyframe")!).OnClick!.Invoke(null!);
        ((NumericInput)inspector.Field("Track Automation 1 Keyframe 1.Gap")!).Value = 1;
        Assert.Equal(1f, first.Keyframes.Keyframes[0].Gap);
        Assert.Empty(second.Keyframes.Keyframes);
        Assert.Null(inspector.Field("Track Automation 2 Keyframe 1.Gap"));

        ((Button)inspector.Field("Track Automation 2.Remove")!).OnClick!.Invoke(null!);
        Assert.Same(first, Assert.Single(track.TrackAutomations));
    }

    [Fact]
    public void TrackAutomationSection_SurvivesASaveLoadRoundTrip()
    {
        var (_, state, inspector) = NewInspector();
        var track = state.AddTrack();
        var manager = new AudioKeyframeManager
        {
            Timing = KeyframeTiming.Time,
            Keyframes = { new AudioKeyframe { Gap = 1.5f, Value = new Modifier(2, ModifierKind.Multiply) } }
        };
        track.AddTrackAutomation(manager, ["kick"]);
        var json = state.SaveProject();

        state.LoadProject(json);
        var loadedTrack = state.Project.Tracks.Single();
        state.SelectTrack(loadedTrack);

        Assert.False(((Checkbox)inspector.Field("Track Automation 1.All sounds")!).Checked);
        Assert.Equal(1.5, ((NumericInput)inspector.Field("Track Automation 1 Keyframe 1.Gap")!).Value);
        Assert.True(((Checkbox)inspector.Field("Track Automation 1 Keyframe 1.Value.Kind")!).Checked);
    }

    [Fact]
    public void LoadingAProject_ShowsTheLoadedValues()
    {
        var (_, state, inspector) = NewInspector();
        var donor = new EditorState();
        donor.Edit(() => donor.Project.Info.Name = "Aleph-0");
        var json = donor.SaveProject();

        state.LoadProject(json);
        inspector.Sync();

        Assert.Equal("Aleph-0", ((TextInput)inspector.Field("Project.Name")!).Value);
    }
}

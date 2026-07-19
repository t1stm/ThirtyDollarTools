using ThirtyDollarConverter.Editor;

namespace EditorScene;

/// <summary>
///     Every editor mutation routes through this class, so the GUI stays a dumb view.
///     Plain state, no GL — unit-tested headless in EditorScene.Tests.
/// </summary>
public class EditorState
{
    /// <summary>Fired after any project mutation (add/remove/rename/new/load).</summary>
    public Action? OnProjectChanged;

    /// <summary>Fired when the selected track changes.</summary>
    public Action<ProjectTrack?>? OnSelectionChanged;

    /// <summary>Fired when the selected clip on the arrangement grid changes.</summary>
    public Action<TrackPlacement?>? OnPlacementSelectionChanged;

    /// <summary>Fired when a channel's mute/solo state changes. Session-only, never saved.</summary>
    public Action? OnChannelsChanged;

    /// <summary>Fired when a track is opened in (or closed from) the note editor.</summary>
    public Action<ProjectTrack?>? OnOpenedTrackChanged;

    /// <summary>Fired when the selected note in the note editor changes.</summary>
    public Action<Note?>? OnNoteSelectionChanged;

    /// <summary>Fired when the selected segment in the note editor changes.</summary>
    public Action<TrackSegment?>? OnSegmentSelectionChanged;

    private readonly HashSet<int> _muted = [];
    private readonly HashSet<int> _soloed = [];

    public ThirtyDollarProject Project { get; private set; } = new();
    public ProjectTrack? SelectedTrack { get; private set; }
    public TrackPlacement? SelectedPlacement { get; private set; }

    /// <summary>The track open in the note editor; null means the arrangement is shown.</summary>
    public ProjectTrack? OpenedTrack { get; private set; }

    public TrackSegment? SelectedSegment { get; private set; }
    public Note? SelectedNote { get; private set; }

    /// <summary>The sound a click in the note editor places. Session-only, never saved.</summary>
    public string? ActiveSound { get; set; }

    public bool Dirty { get; private set; }
    public bool IsCurrentlyPlayingAudio { get; set; }

    /// <summary>Where the project lives on disk; null until first saved or loaded from a file.</summary>
    public string? ProjectPath { get; private set; }

    public ProjectTrack AddTrack()
    {
        var track = Project.NewTrack();
        Touch();
        return track;
    }

    public bool RemoveTrack(ProjectTrack track)
    {
        if (!Project.RemoveTrack(track)) return false;
        if (SelectedTrack == track) SelectTrack(null);
        if (SelectedPlacement?.Track == track) SelectPlacement(null); // cascaded away
        if (OpenedTrack == track) CloseTrack();
        Touch();
        return true;
    }

    public void OpenTrack(ProjectTrack track)
    {
        if (OpenedTrack == track) return;
        OpenedTrack = track;
        SelectSegment(track.Segments[0]);
        SelectNote(null);
        ActiveSound ??= track.Segments.SelectMany(s => s.Notes).FirstOrDefault()?.Sound;
        OnOpenedTrackChanged?.Invoke(track);
    }

    public void CloseTrack()
    {
        if (OpenedTrack == null) return;
        OpenedTrack = null;
        SelectSegment(null);
        SelectNote(null);
        OnOpenedTrackChanged?.Invoke(null);
    }

    public void SelectSegment(TrackSegment? segment)
    {
        if (SelectedSegment == segment) return;
        SelectedSegment = segment;
        OnSegmentSelectionChanged?.Invoke(segment);
    }

    public void SelectNote(Note? note)
    {
        if (SelectedNote == note) return;
        SelectedNote = note;
        OnNoteSelectionChanged?.Invoke(note);
    }

    public Note AddNote(TrackSegment segment, int step, string sound, double value)
    {
        var note = new Note { Step = step, Sound = sound, Value = value };
        segment.Notes.Add(note);
        Touch();
        return note;
    }

    public void MoveNote(TrackSegment from, TrackSegment to, Note note, int step, double value)
    {
        if (from == to && note.Step == step && note.Value == value) return;
        if (from != to)
        {
            from.Notes.Remove(note);
            to.Notes.Add(note);
        }

        note.Step = step;
        note.Value = value;
        Touch();
    }

    public bool RemoveNote(TrackSegment segment, Note note)
    {
        if (!segment.Notes.Remove(note)) return false;
        if (SelectedNote == note) SelectNote(null);
        Touch();
        return true;
    }

    public TrackSegment AddSegment(ProjectTrack track)
    {
        var segment = track.NewSegment();
        Touch();
        return segment;
    }

    /// <summary>Removes a segment; refuses on the track's last one (library invariant).</summary>
    public bool RemoveSegment(ProjectTrack track, TrackSegment segment)
    {
        if (!track.RemoveSegment(segment)) return false;
        if (SelectedNote != null && segment.Notes.Contains(SelectedNote)) SelectNote(null);
        if (SelectedSegment == segment) SelectSegment(track.Segments[0]);
        Touch();
        return true;
    }

    public TrackPlacement PlaceTrack(ProjectTrack track, int channel, double startQuarterNotes)
    {
        var placement = Project.Place(track, channel, startQuarterNotes);
        Touch();
        return placement;
    }

    public void MovePlacement(TrackPlacement placement, int channel, double startQuarterNotes)
    {
        if (placement.Channel == channel && placement.StartQuarterNotes == startQuarterNotes) return;
        placement.Channel = channel;
        placement.StartQuarterNotes = startQuarterNotes;
        Touch();
    }

    public bool RemovePlacement(TrackPlacement placement)
    {
        if (!Project.RemovePlacement(placement)) return false;
        if (SelectedPlacement == placement) SelectPlacement(null);
        Touch();
        return true;
    }

    public void SelectPlacement(TrackPlacement? placement)
    {
        if (SelectedPlacement == placement) return;
        SelectedPlacement = placement;
        OnPlacementSelectionChanged?.Invoke(placement);
    }

    /// <summary>
    ///     Applies an inspector field edit (segment/note/project numbers and texts) so it
    ///     dirties and notifies like every other mutation. Edits with real semantics
    ///     (rename, timing follow) keep their own methods.
    /// </summary>
    public void Edit(Action edit)
    {
        edit();
        Touch();
    }

    /// <summary>True when the track shares the project's timing instance and follows its tempo.</summary>
    public bool TrackFollowsRootTiming(ProjectTrack track)
    {
        return ReferenceEquals(track.Timing, Project.RootTiming);
    }

    /// <summary>
    ///     Following shares the root TimingInfo instance (the save format's null-timing
    ///     semantics); unfollowing copies the current values into an own instance, so
    ///     nothing audibly changes until the copy is edited.
    /// </summary>
    public void SetTrackFollowsRootTiming(ProjectTrack track, bool follows)
    {
        if (follows == TrackFollowsRootTiming(track)) return;
        var timing = track.Timing;
        track.Timing = follows
            ? Project.RootTiming
            : new TimingInfo { BPM = timing.BPM, Numerator = timing.Numerator, Denominator = timing.Denominator };
        Touch();
    }

    public void RenameTrack(ProjectTrack track, string name)
    {
        if (track.Name == name) return;
        track.Name = name;
        Touch();
    }

    public void SelectTrack(ProjectTrack? track)
    {
        if (SelectedTrack == track) return;
        SelectedTrack = track;
        OnSelectionChanged?.Invoke(track);
    }

    public void ToggleMute(int channel)
    {
        if (!_muted.Add(channel)) _muted.Remove(channel);
        OnChannelsChanged?.Invoke();
    }

    public void ToggleSolo(int channel)
    {
        if (!_soloed.Add(channel)) _soloed.Remove(channel);
        OnChannelsChanged?.Invoke();
    }

    public bool IsMuted(int channel)
    {
        return _muted.Contains(channel);
    }

    public bool IsSoloed(int channel)
    {
        return _soloed.Contains(channel);
    }

    /// <summary>FL semantics: any solo wins; otherwise everything not muted sounds.</summary>
    public bool IsChannelAudible(int channel)
    {
        return _soloed.Count > 0 ? _soloed.Contains(channel) : !_muted.Contains(channel);
    }

    public void NewProject()
    {
        Replace(new ThirtyDollarProject());
    }

    public void LoadProject(string json)
    {
        Replace(ProjectFile.Load(json));
    }

    public string SaveProject()
    {
        var json = ProjectFile.Save(Project);
        Dirty = false;
        return json;
    }

    public void LoadProjectFromFile(string path)
    {
        Replace(ProjectFile.Load(File.ReadAllText(path)));
        ProjectPath = path;
    }

    public void SaveProjectToFile(string path)
    {
        File.WriteAllText(path, SaveProject());
        ProjectPath = path;
    }

    private void Replace(ThirtyDollarProject project)
    {
        Project = project;
        Dirty = false;
        ProjectPath = null;
        _muted.Clear();
        _soloed.Clear();
        ActiveSound = null;
        CloseTrack();
        SelectTrack(null);
        SelectPlacement(null);
        OnProjectChanged?.Invoke();
    }

    private void Touch()
    {
        Dirty = true;
        OnProjectChanged?.Invoke();
    }
}

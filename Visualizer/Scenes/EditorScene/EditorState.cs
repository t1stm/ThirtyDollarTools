using ThirtyDollarConverter.Editor;

namespace EditorScene;

/// <summary>
///     Every editor mutation routes through this class, so the GUI stays a dumb view.
///     Plain state, no GL — unit-tested headless in EditorScene.Tests.
/// </summary>
public class EditorState
{
    /// <summary>Fired after any project mutation (add/remove/rename/new/load).</summary>
    public event Action? OnProjectChanged;

    /// <summary>Fired when the selected track changes.</summary>
    public event Action<ProjectTrack?>? OnSelectionChanged;

    /// <summary>Fired when the selected clip on the arrangement grid changes.</summary>
    public event Action<TrackPlacement?>? OnPlacementSelectionChanged;

    /// <summary>Fired when a channel's mute/solo state changes. Session-only, never saved.</summary>
    public event Action? OnChannelsChanged;

    /// <summary>Fired when a track is opened in (or closed from) the note editor.</summary>
    public event Action<ProjectTrack?>? OnOpenedTrackChanged;

    /// <summary>Fired when the selected note in the note editor changes.</summary>
    public event Action<Note?>? OnNoteSelectionChanged;

    /// <summary>Fired when the selected segment in the note editor changes.</summary>
    public event Action<TrackSegment?>? OnSegmentSelectionChanged;

    /// <summary>Fired after an instrument is added/renamed/edited/removed.</summary>
    public event Action? OnInstrumentsChanged;

    private readonly MuteSolo _muteSolo = new();
    private readonly UndoHistory _undoHistory = new();

    public ThirtyDollarProject Project { get; private set; } = new();
    public ProjectTrack? SelectedTrack { get; private set; }
    public TrackPlacement? SelectedPlacement { get; private set; }

    /// <summary>The track open in the note editor; null means the arrangement is shown.</summary>
    public ProjectTrack? OpenedTrack { get; private set; }

    public TrackSegment? SelectedSegment { get; private set; }
    public Note? SelectedNote { get; private set; }

    /// <summary>The instrument a click in the note editor places. Session-only, never saved.</summary>
    public Instrument? ActiveInstrument { get; set; }

    /// <summary>
    ///     Raises <see cref="OnInstrumentsChanged" /> for callers outside this class — a
    ///     plain <see cref="ActiveInstrument" /> set fires no event of its own, but the
    ///     active-instrument button still needs to refresh (see
    ///     <see cref="Scenes.Components.InstrumentWorkflow" />'s pick flow).
    /// </summary>
    public void NotifyInstrumentsChanged()
    {
        OnInstrumentsChanged?.Invoke();
    }

    /// <summary>
    ///     Modifiers (volume/pan/offset/automation, never value) copied from the last-clicked
    ///     note; new notes placed afterwards inherit them. Cleared when the note editor closes.
    /// </summary>
    public Note? CopiedModifiers { get; private set; }

    public bool Dirty { get; private set; }
    public bool IsCurrentlyPlayingAudio { get; set; }

    public bool CanUndo => _undoHistory.CanUndo;
    public bool CanRedo => _undoHistory.CanRedo;

    /// <summary>Where the project lives on disk; null until first saved or loaded from a file.</summary>
    public string? ProjectPath { get; private set; }

    public ProjectTrack AddTrack()
    {
        var track = Project.NewTrack();
        Touch();
        return track;
    }

    /// <summary>Duplicates a track under the given name, deep-copied so editing the copy never reaches the source.</summary>
    public ProjectTrack DuplicateTrack(ProjectTrack track, string name)
    {
        var copy = Project.DuplicateTrack(track, name);
        SelectTrack(copy);
        Touch();
        return copy;
    }

    public bool RemoveTrack(ProjectTrack track)
    {
        var index = IndexOf(Project.Tracks, track);
        var cascadedPlacements = Project.Placements.Where(p => p.Track == track).ToArray();
        if (!Project.RemoveTrack(track)) return false;
        if (SelectedTrack == track) SelectTrack(null);
        if (SelectedPlacement?.Track == track) SelectPlacement(null); // cascaded away
        if (OpenedTrack == track) CloseTrack();

        _undoHistory.Push(
            undo: () =>
            {
                Project.AddTrack(track, index);
                foreach (var placement in cascadedPlacements) Project.AddPlacement(placement);
            },
            redo: () => Project.RemoveTrack(track));
        Touch();
        return true;
    }

    public void OpenTrack(ProjectTrack track)
    {
        if (OpenedTrack == track) return;
        OpenedTrack = track;
        SelectSegment(track.Segments[0]);
        SelectNote(null);
        ActiveInstrument ??= track.Segments.SelectMany(s => s.Notes).FirstOrDefault()?.Instrument
            ?? Project.Instruments.FirstOrDefault();
        OnOpenedTrackChanged?.Invoke(track);
    }

    public void CloseTrack()
    {
        if (OpenedTrack == null) return;
        OpenedTrack = null;
        SelectSegment(null);
        SelectNote(null);
        CopiedModifiers = null;
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
        if (note != null) CopiedModifiers = note;
        OnNoteSelectionChanged?.Invoke(note);
    }

    public Note AddNote(TrackSegment segment, int step, Instrument instrument, double value)
    {
        var note = new Note
        {
            Step = step,
            Instrument = instrument,
            Value = value,
            Volume = CopiedModifiers?.Volume,
            Pan = CopiedModifiers?.Pan ?? 0,
            Offset = CopiedModifiers?.Offset ?? 0,
            Automation = CopiedModifiers?.Automation?.Clone()
        };
        segment.Notes.Add(note);
        _undoHistory.Push(
            undo: () => segment.Notes.Remove(note),
            redo: () => segment.Notes.Add(note));
        Touch();
        return note;
    }

    public Instrument AddInstrument(string name)
    {
        var instrument = Project.NewInstrument(name);
        Touch();
        OnInstrumentsChanged?.Invoke();
        return instrument;
    }

    public void RenameInstrument(Instrument instrument, string name)
    {
        if (instrument.Name == name) return;
        instrument.Name = name;
        Touch();
        OnInstrumentsChanged?.Invoke();
    }

    public void SetInstrumentSounds(Instrument instrument, IEnumerable<string> sounds,
        IReadOnlyDictionary<string, SoundAdjustment>? adjustments = null)
    {
        instrument.Sounds.Clear();
        instrument.Sounds.AddRange(sounds);

        instrument.Adjustments.Clear();
        if (adjustments != null)
            foreach (var (sound, adjustment) in adjustments)
                if (!adjustment.IsNoOp)
                    instrument.Adjustments[sound] = adjustment;

        Touch();
        OnInstrumentsChanged?.Invoke();
    }

    /// <summary>Refuses while any note still references the instrument (library invariant).</summary>
    public bool RemoveInstrument(Instrument instrument)
    {
        if (!Project.RemoveInstrument(instrument)) return false;
        if (ActiveInstrument == instrument) ActiveInstrument = Project.Instruments.FirstOrDefault();
        Touch();
        OnInstrumentsChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     Deletes the instrument outright: every note using it, in every track, is removed
    ///     first so <see cref="RemoveInstrument" />'s reference guard never refuses. Used by
    ///     the explicit "delete instrument" action (after user confirmation), as opposed to
    ///     <see cref="RemoveInstrument" />'s safe default.
    /// </summary>
    public void DeleteInstrumentEverywhere(Instrument instrument)
    {
        var removedNotes = new List<(TrackSegment Segment, Note Note, int Index)>();
        foreach (var segment in Project.Tracks.SelectMany(track => track.Segments))
            for (var i = segment.Notes.Count - 1; i >= 0; i--)
            {
                if (segment.Notes[i].Instrument != instrument) continue;
                removedNotes.Add((segment, segment.Notes[i], i));
                segment.Notes.RemoveAt(i);
            }
        removedNotes.Reverse(); // restore left-to-right

        if (SelectedNote?.Instrument == instrument) SelectNote(null);

        var instrumentIndex = IndexOf(Project.Instruments, instrument);
        _undoHistory.Push(
            undo: () =>
            {
                Project.AddInstrument(instrument, instrumentIndex);
                foreach (var (segment, note, index) in removedNotes)
                    segment.Notes.Insert(Math.Clamp(index, 0, segment.Notes.Count), note);
            },
            redo: () =>
            {
                foreach (var (segment, note, _) in removedNotes) segment.Notes.Remove(note);
                RemoveInstrument(instrument);
            });

        RemoveInstrument(instrument);
    }

    public void MoveNote(TrackSegment from, TrackSegment to, Note note, int step, double value)
    {
        if (from == to && note.Step == step && note.Value == value) return;
        var (prevSegment, prevStep, prevValue) = (from, note.Step, note.Value);

        if (from != to)
        {
            from.Notes.Remove(note);
            to.Notes.Add(note);
        }

        note.Step = step;
        note.Value = value;

        _undoHistory.PushOrMergeMove(note,
            undo: () =>
            {
                if (to != prevSegment)
                {
                    to.Notes.Remove(note);
                    prevSegment.Notes.Add(note);
                }

                note.Step = prevStep;
                note.Value = prevValue;
            },
            redo: () =>
            {
                if (prevSegment != to)
                {
                    prevSegment.Notes.Remove(note);
                    to.Notes.Add(note);
                }

                note.Step = step;
                note.Value = value;
            });
        Touch();
    }

    public bool RemoveNote(TrackSegment segment, Note note)
    {
        if (!segment.Notes.Remove(note)) return false;
        if (SelectedNote == note) SelectNote(null);
        _undoHistory.Push(
            undo: () => segment.Notes.Add(note),
            redo: () => segment.Notes.Remove(note));
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
        var index = IndexOf(track.Segments, segment);
        if (!track.RemoveSegment(segment)) return false;
        if (SelectedNote != null && segment.Notes.Contains(SelectedNote)) SelectNote(null);
        if (SelectedSegment == segment) SelectSegment(track.Segments[0]);

        _undoHistory.Push(
            undo: () => track.AddSegment(segment, index),
            redo: () => track.RemoveSegment(segment));
        Touch();
        return true;
    }

    public TrackPlacement PlaceTrack(ProjectTrack track, int channel, double startQuarterNotes)
    {
        var placement = Project.Place(track, channel, startQuarterNotes);
        _undoHistory.Push(
            undo: () => Project.RemovePlacement(placement),
            redo: () => Project.AddPlacement(placement));
        Touch();
        return placement;
    }

    public void MovePlacement(TrackPlacement placement, int channel, double startQuarterNotes)
    {
        if (placement.Channel == channel && placement.StartQuarterNotes == startQuarterNotes) return;
        var (prevChannel, prevStart) = (placement.Channel, placement.StartQuarterNotes);

        placement.Channel = channel;
        placement.StartQuarterNotes = startQuarterNotes;

        _undoHistory.PushOrMergeMove(placement,
            undo: () =>
            {
                placement.Channel = prevChannel;
                placement.StartQuarterNotes = prevStart;
            },
            redo: () =>
            {
                placement.Channel = channel;
                placement.StartQuarterNotes = startQuarterNotes;
            });
        Touch();
    }

    public bool RemovePlacement(TrackPlacement placement)
    {
        if (!Project.RemovePlacement(placement)) return false;
        if (SelectedPlacement == placement) SelectPlacement(null);
        _undoHistory.Push(
            undo: () => Project.AddPlacement(placement),
            redo: () => Project.RemovePlacement(placement));
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
        _muteSolo.ToggleMute(channel);
        OnChannelsChanged?.Invoke();
    }

    public void ToggleSolo(int channel)
    {
        _muteSolo.ToggleSolo(channel);
        OnChannelsChanged?.Invoke();
    }

    public bool IsMuted(int channel)
    {
        return _muteSolo.IsMuted(channel);
    }

    public bool IsSoloed(int channel)
    {
        return _muteSolo.IsSoloed(channel);
    }

    /// <summary>FL semantics: any solo wins; otherwise everything not muted sounds.</summary>
    public bool IsChannelAudible(int channel)
    {
        return _muteSolo.IsChannelAudible(channel);
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

    /// <summary>Marks the start of a new drag gesture, so a run of MoveNote/MovePlacement
    /// calls on the same object (one drag, many frames) collapses into a single undo step.</summary>
    public void BeginGesture()
    {
        _undoHistory.BeginGesture();
    }

    public void Undo()
    {
        if (!_undoHistory.Undo()) return;
        SelectNote(null);
        SelectPlacement(null);
        Touch();
    }

    public void Redo()
    {
        if (!_undoHistory.Redo()) return;
        SelectNote(null);
        SelectPlacement(null);
        Touch();
    }

    private void Replace(ThirtyDollarProject project)
    {
        Project = project;
        Dirty = false;
        ProjectPath = null;
        _muteSolo.Clear();
        _undoHistory.Clear();
        ActiveInstrument = null;
        CloseTrack();
        SelectTrack(null);
        SelectPlacement(null);
        OnProjectChanged?.Invoke();
        OnInstrumentsChanged?.Invoke();
    }

    private void Touch()
    {
        Dirty = true;
        OnProjectChanged?.Invoke();
    }

    private static int IndexOf<T>(IReadOnlyList<T> list, T item)
    {
        for (var i = 0; i < list.Count; i++)
            if (Equals(list[i], item))
                return i;
        return -1;
    }
}

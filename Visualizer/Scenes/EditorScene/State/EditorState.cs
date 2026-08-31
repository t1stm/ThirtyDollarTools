using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.State;

/// <summary>
///     The active editing tool, shared by both editors. The views' input handlers branch
///     on <see cref="EditorState.ActiveTool" /> directly.
/// </summary>
// ponytail: tool dispatch is an enum branch in the two views; extract an IEditorTool
// (per-tool press/drag/release/key handlers) when a third tool with nontrivial
// behaviour lands (slice, zoom, mute-paint).
public enum EditorTool
{
    Draw,
    Select
}

/// <summary>
///     Every editor mutation routes through this class, so the GUI stays a dumb view.
///     Plain state, no GL - unit-tested headless in EditorScene.Tests.
///     Split by subject across <c>EditorState.Selection</c>, <c>.Tracks</c>, <c>.Notes</c>,
///     <c>.Arrangement</c>, <c>.Faithful</c>, <c>.Instruments</c> and <c>.Clipboard</c>.
///     This part holds the project itself, undo/redo, and every event the other parts fire.
/// </summary>
public partial class EditorState
{
    private readonly MuteSolo _muteSolo = new();
    private readonly UndoHistory _undoHistory = new();

    public ThirtyDollarProject Project { get; private set; } = new();

    /// <summary>
    ///     Draw (paint/place, single selection on click) or Select (marquee,
    ///     multi-selection). Switching tools keeps the current selection.
    /// </summary>
    public EditorTool ActiveTool
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnToolChanged?.Invoke(value);
        }
    } = EditorTool.Draw;

    public bool Dirty { get; private set; }
    public bool IsCurrentlyPlayingAudio { get; set; }

    public bool CanUndo => _undoHistory.CanUndo;
    public bool CanRedo => _undoHistory.CanRedo;

    /// <summary>Where the project lives on disk; null until first saved or loaded from a file.</summary>
    public string? ProjectPath { get; private set; }

    public bool AnySoloed => _muteSolo.AnySoloed;

    /// <summary>Fired after any project mutation (add/remove/rename/new/load).</summary>
    public event Action? OnProjectChanged;

    /// <summary>Fired when the selected track changes.</summary>
    public event Action<ProjectTrack?>? OnSelectionChanged;

    /// <summary>
    ///     Fired when the placement selection changes, with the derived single
    ///     value (see <see cref="SelectedPlacement" />). Fired once per batch mutation.
    /// </summary>
    public event Action<TrackPlacement?>? OnPlacementSelectionChanged;

    /// <summary>Fired when a channel's mute/solo state changes. Session-only, never saved.</summary>
    public event Action? OnChannelsChanged;

    /// <summary>Fired when a track is opened in (or closed from) the note editor.</summary>
    public event Action<ProjectTrack?>? OnOpenedTrackChanged;

    /// <summary>
    ///     Fired when the note selection changes, with the derived single value
    ///     (see <see cref="SelectedNote" />). Fired once per batch mutation.
    /// </summary>
    public event Action<Note?>? OnNoteSelectionChanged;

    /// <summary>Fired when the selected segment in the note editor changes.</summary>
    public event Action<TrackSegment?>? OnSegmentSelectionChanged;

    /// <summary>
    ///     Fired when the faithful item selection changes, with the derived single value
    ///     (see <see cref="SelectedItem" />). Fired once per batch mutation.
    /// </summary>
    public event Action<FaithfulItem?>? OnItemSelectionChanged;

    /// <summary>Fired after an instrument is added/renamed/edited/removed.</summary>
    public event Action? OnInstrumentsChanged;

    /// <summary>Fired when the active tool (Draw/Select) changes.</summary>
    public event Action<EditorTool>? OnToolChanged;

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

    // ---------------------------------------------------------------- mute / solo

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

    /// <summary>Any soloed channel silences the rest; with nothing soloed, every unmuted channel sounds.</summary>
    public bool IsChannelAudible(int channel)
    {
        return _muteSolo.IsChannelAudible(channel);
    }

    // ---------------------------------------------------------------- project lifecycle

    public void NewProject()
    {
        Replace(new ThirtyDollarProject());
    }

    public void LoadProject(string json)
    {
        Replace(ProjectFile.Load(json));
    }

    /// <summary>
    ///     Imports a TDW sequence as a whole new project, replacing the open one.
    ///     Not undoable (Replace clears undo history) - the caller confirms the discard
    ///     first. Unlike a load, the result exists only in memory: stays dirty (so the
    ///     unsaved-changes guard still fires on exit) and keeps <see cref="ProjectPath" />
    ///     null (so Save asks for a location).
    /// </summary>
    public ImportResult ReplaceWithImportedProject(Sequence sequence, string name,
        IReadOnlyDictionary<string, Sound>? soundMap)
    {
        var result = SequenceImporter.ToProject(sequence, name, soundMap, out var project);
        Replace(project);
        Touch();
        return result;
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
        _muteSolo.Clear();
        _undoHistory.Clear();
        _clipboard.Clear();
        _lastInstrumentByTrack.Clear();
        CloseTrack();
        ActiveInstrument = null;
        SelectTrack(null);
        ClearSelection();
        OnProjectChanged?.Invoke();
        OnInstrumentsChanged?.Invoke();
    }

    // ---------------------------------------------------------------- undo / redo

    /// <summary>
    ///     Marks the start of a new drag gesture, so a run of MoveNote/MovePlacement calls on
    ///     the same object across the drag's frames collapses into a single undo step.
    /// </summary>
    public void BeginGesture()
    {
        _undoHistory.BeginGesture();
    }

    public void Undo()
    {
        if (!_undoHistory.Undo()) return;
        PruneDroppedTracks();
        PruneSelection();
        Touch();
    }

    public void Redo()
    {
        if (!_undoHistory.Redo()) return;
        PruneDroppedTracks();
        PruneSelection();
        Touch();
    }

    /// <summary>
    ///     Drops what the last undo/redo removed from the selection and keeps the rest, so
    ///     undoing a delete hands the restored items back still selected.
    /// </summary>
    private void PruneSelection()
    {
        if (OpenedFaithfulTrack is { } faithful) _items.Keep(faithful.Items.Contains);
        else if (OpenedTrack is { } track)
            _notes.Keep(note => track.Segments.Any(segment => segment.Notes.Contains(note)));
        else _placements.Keep(Project.Placements.Contains);
    }

    /// <summary>
    ///     Drops references to tracks the last undo/redo took out of the project. Those steps
    ///     replay <see cref="ThirtyDollarProject" /> mutations directly, so unlike
    ///     <see cref="RemoveTrack" /> they never run its cleanup: without this the list can
    ///     highlight a track the project no longer has, and the next arrangement click hands
    ///     it to Place, which rejects foreign tracks.
    /// </summary>
    private void PruneDroppedTracks()
    {
        if (OpenedTrack is { } opened && !Project.Tracks.Contains(opened)) CloseTrack();

        _tracks.Keep(Project.Tracks.Contains);
        // Same dangling clipboard entries RemoveTrack prunes - a paste would hit Place too.
        PruneClipboardPlacements(Project.Tracks.Contains);
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

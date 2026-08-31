using ThirtyDollarConverter.Editor;

namespace EditorScene.State;

/// <summary>
///     The four editor selections and the public API the views drive them through. Each is a
///     <see cref="Selection{T}" />; this part is the wiring from their change events to the
///     public On*Changed ones, plus the named forwarders the views already call.
/// </summary>
public partial class EditorState
{
    private readonly Selection<FaithfulItem> _items = new();
    private readonly Selection<Note> _notes = new();
    private readonly Selection<TrackPlacement> _placements = new();
    private readonly Selection<ProjectTrack> _tracks = new();

    public EditorState()
    {
        _tracks.Changed += track => OnSelectionChanged?.Invoke(track);
        _placements.Changed += placement => OnPlacementSelectionChanged?.Invoke(placement);
        _items.Changed += item => OnItemSelectionChanged?.Invoke(item);
        _notes.Changed += note =>
        {
            // Set before the event: subscribers read CopiedModifiers off a single-note pick.
            if (_notes.Count == 1) CopiedModifiers = _notes.Items[0];
            OnNoteSelectionChanged?.Invoke(note);
        };
    }

    /// <summary>Every currently selected track, in selection order. Ctrl/Cmd-click adds/removes one.</summary>
    public IReadOnlyList<ProjectTrack> SelectedTracks => _tracks.Items;

    /// <summary>
    ///     Derived view: non-null only when exactly one track is selected, same as
    ///     <see cref="SelectedPlacement" />. The single-selection consumers (inspector form,
    ///     clip placing, arrangement highlight) read this and go quiet on a multi-selection.
    /// </summary>
    public ProjectTrack? SelectedTrack => _tracks.Single;

    /// <summary>Every currently selected placement, in selection order (last = primary).</summary>
    public IReadOnlyList<TrackPlacement> SelectedPlacements => _placements.Items;

    /// <summary>
    ///     Derived view: non-null only when exactly one placement is selected.
    ///     Single-selection consumers (arrangement highlight, cascades) read this.
    /// </summary>
    public TrackPlacement? SelectedPlacement => _placements.Single;

    /// <summary>Every currently selected note, in selection order (last = primary).</summary>
    public IReadOnlyList<Note> SelectedNotes => _notes.Items;

    /// <summary>
    ///     Derived view: non-null only when exactly one note is selected.
    ///     Single-selection consumers (inspector form, CopiedModifiers) read this.
    /// </summary>
    public Note? SelectedNote => _notes.Single;

    /// <summary>
    ///     The selected slots of a faithful sequence - one under the Draw tool, any number
    ///     under Select. The faithful counterpart of <see cref="SelectedNotes" />.
    /// </summary>
    public IReadOnlyList<FaithfulItem> SelectedItems => _items.Items;

    /// <summary>
    ///     The one selected slot, the inspector's subject there - null while nothing or more
    ///     than one is selected, exactly as <see cref="SelectedNote" />.
    /// </summary>
    public FaithfulItem? SelectedItem => _items.Single;

    /// <summary>
    ///     Modifiers (volume/pan/offset/automation, never value) copied from the last-clicked
    ///     note; new notes placed afterwards inherit them. Cleared when the note editor closes.
    /// </summary>
    public Note? CopiedModifiers { get; private set; }

    // ---------------------------------------------------------------- tracks

    /// <summary>Replaces the whole track selection with one track (or clears it).</summary>
    public void SelectTrack(ProjectTrack? track)
    {
        _tracks.SetOne(track);
    }

    /// <summary>
    ///     Replaces the whole track selection. Fires <see cref="OnSelectionChanged" /> once,
    ///     with the derived single value (see <see cref="SelectedTrack" />).
    /// </summary>
    public void SetTrackSelection(IEnumerable<ProjectTrack> tracks)
    {
        _tracks.Set(tracks);
    }

    /// <summary>Ctrl/Cmd-click: adds the track to the selection, or removes it if it is already in.</summary>
    public void ToggleTrackSelection(ProjectTrack track)
    {
        _tracks.Toggle(track);
    }

    // ---------------------------------------------------------------- placements

    /// <summary>Replaces the placement selection with a single placement, or clears it when null.</summary>
    public void SelectPlacement(TrackPlacement? placement)
    {
        _placements.SetOne(placement);
    }

    /// <summary>
    ///     Replaces the whole placement selection. Fires <see cref="OnPlacementSelectionChanged" />
    ///     once, even for multi-placement selections.
    /// </summary>
    public void SetPlacementSelection(IEnumerable<TrackPlacement> placements)
    {
        _placements.Set(placements);
    }

    /// <summary>Appends placements not already selected. No-op for ones already present.</summary>
    public void AddToPlacementSelection(IEnumerable<TrackPlacement> placements)
    {
        _placements.Add(placements);
    }

    /// <summary>Removes placements from the selection. No-op for ones not present.</summary>
    public void RemoveFromPlacementSelection(IEnumerable<TrackPlacement> placements)
    {
        _placements.Remove(placements);
    }

    // ---------------------------------------------------------------- notes

    /// <summary>Replaces the note selection with a single note, or clears it when null.</summary>
    public void SelectNote(Note? note)
    {
        _notes.SetOne(note);
    }

    /// <summary>
    ///     Replaces the whole note selection. Fires <see cref="OnNoteSelectionChanged" />
    ///     once, even for multi-note selections; subscribers read the derived
    ///     <see cref="SelectedNote" />, which is non-null only for a single note.
    /// </summary>
    public void SetNoteSelection(IEnumerable<Note> notes)
    {
        _notes.Set(notes);
    }

    /// <summary>
    ///     Appends notes not already selected. No-op for ones already present
    ///     (append semantics, not toggle).
    /// </summary>
    public void AddToNoteSelection(IEnumerable<Note> notes)
    {
        _notes.Add(notes);
    }

    /// <summary>Removes notes from the selection. No-op for ones not present.</summary>
    public void RemoveFromNoteSelection(IEnumerable<Note> notes)
    {
        _notes.Remove(notes);
    }

    // ---------------------------------------------------------------- faithful items

    /// <summary>Replaces the item selection with a single slot, or clears it when null.</summary>
    public void SelectItem(FaithfulItem? item)
    {
        _items.SetOne(item);
    }

    /// <summary>Replaces the whole item selection. Fires <see cref="OnItemSelectionChanged" /> once.</summary>
    public void SetItemSelection(IEnumerable<FaithfulItem> items)
    {
        _items.Set(items);
    }

    /// <summary>Adds the item to the selection, or drops it when it is already in - Select's click.</summary>
    public void ToggleItemSelection(FaithfulItem item)
    {
        _items.Toggle(item);
    }

    // ---------------------------------------------------------------- all of them

    /// <summary>
    ///     Selects every note of the opened track (all segments) when a track is
    ///     open, otherwise every placement on the arrangement.
    /// </summary>
    public void SelectAll()
    {
        if (OpenedFaithfulTrack is { } faithful) _items.Set(faithful.Items);
        else if (OpenedTrack is { } track) _notes.Set(track.Segments.SelectMany(segment => segment.Notes));
        else _placements.Set(Project.Placements);
    }

    /// <summary>Clears the note, placement and faithful-item selections.</summary>
    public void ClearSelection()
    {
        _notes.Set([]);
        _placements.Set([]);
        _items.Set([]);
    }
}

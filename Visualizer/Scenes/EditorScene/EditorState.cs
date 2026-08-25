using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Dialogs;
using JetBrains.Annotations;

namespace EditorScene;

/// <summary>
///     The active editing tool, shared by both editors. Two tools with three
///     branch points each do not justify an IEditorTool strategy interface - the views'
///     input handlers branch on <see cref="EditorState.ActiveTool" /> directly.
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
/// </summary>
public class EditorState
{
    private readonly EditorClipboard _clipboard = new();
    private readonly Dictionary<ProjectTrack, Instrument?> _lastInstrumentByTrack = [];

    private readonly MuteSolo _muteSolo = new();
    private readonly List<FaithfulItem> _selectedItems = [];
    private readonly List<Note> _selectedNotes = [];
    private readonly List<TrackPlacement> _selectedPlacements = [];
    private readonly List<ProjectTrack> _selectedTracks = [];
    private readonly UndoHistory _undoHistory = new();

    public ThirtyDollarProject Project { get; private set; } = new();

    /// <summary>Every currently selected track, in selection order. Ctrl/Cmd-click adds/removes one.</summary>
    public IReadOnlyList<ProjectTrack> SelectedTracks => _selectedTracks;

    /// <summary>
    ///     Derived view: non-null only when exactly one track is selected, same as
    ///     <see cref="SelectedPlacement" />. The single-selection consumers (inspector form,
    ///     clip placing, arrangement highlight) read this and go quiet on a multi-selection.
    /// </summary>
    public ProjectTrack? SelectedTrack => _selectedTracks.Count == 1 ? _selectedTracks[0] : null;

    /// <summary>Every currently selected placement, in selection order (last = primary).</summary>
    public IReadOnlyList<TrackPlacement> SelectedPlacements => _selectedPlacements;

    /// <summary>
    ///     Derived view: non-null only when exactly one placement is selected.
    ///     Existing single-selection consumers (arrangement highlight, cascades) read this.
    /// </summary>
    public TrackPlacement? SelectedPlacement => _selectedPlacements.Count == 1 ? _selectedPlacements[0] : null;

    /// <summary>The track open in the note editor; null means the arrangement is shown.</summary>
    public ProjectTrack? OpenedTrack { get; private set; }

    public TrackSegment? SelectedSegment { get; private set; }

    /// <summary>Every currently selected note, in selection order (last = primary).</summary>
    public IReadOnlyList<Note> SelectedNotes => _selectedNotes;

    /// <summary>
    ///     Derived view: non-null only when exactly one note is selected.
    ///     Existing single-selection consumers (inspector form, CopiedModifiers) read this.
    /// </summary>
    public Note? SelectedNote => _selectedNotes.Count == 1 ? _selectedNotes[0] : null;

    /// <summary>
    ///     The instrument a click in the note editor places. Session-only, never saved.
    ///     Remembered per track (see <see cref="OpenTrack" />), so switching back to a track
    ///     restores whichever instrument was last active in it.
    /// </summary>
    public Instrument? ActiveInstrument
    {
        get;
        set
        {
            field = value;
            if (OpenedTrack is { } track) _lastInstrumentByTrack[track] = value;
        }
    }

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

    /// <summary>Fired after an instrument is added/renamed/edited/removed.</summary>
    public event Action? OnInstrumentsChanged;

    /// <summary>Fired when the active tool (Draw/Select) changes.</summary>
    public event Action<EditorTool>? OnToolChanged;

    /// <summary>
    ///     Raises <see cref="OnInstrumentsChanged" /> for callers outside this class - a
    ///     plain <see cref="ActiveInstrument" /> set fires no event of its own, but the
    ///     active-instrument button still needs to refresh (see
    ///     <see cref="Scenes.Components.InstrumentWorkflow" />'s pick flow).
    /// </summary>
    public void NotifyInstrumentsChanged()
    {
        OnInstrumentsChanged?.Invoke();
    }

    public ProjectTrack AddTrack(TrackKind kind = TrackKind.PianoRoll)
    {
        var track = Project.NewTrack(kind);
        Touch();
        return track;
    }

    /// <summary>Duplicates a track under the given name, deep-copied so editing the copy never reaches the source.</summary>
    [UsedImplicitly]
    public ProjectTrack DuplicateTrack(ProjectTrack track, string name)
    {
        var copy = Project.DuplicateTrack(track, name);
        SelectTrack(copy);
        Touch();
        return copy;
    }

    /// <summary>
    ///     Reorders the selected tracks as one block, dropping them onto
    ///     <paramref name="hovered" /> - the row under the pointer during a drag of the
    ///     handle in <see cref="Scenes.Layout.TrackListPanel" />. The block keeps the list's
    ///     order, not the order the tracks were clicked in, and lands above the hovered row
    ///     when dragged upwards, below it when dragged downwards. Merges into one undo
    ///     entry per drag gesture, same as a placement drag.
    /// </summary>
    public void MoveSelectedTracks(ProjectTrack hovered)
    {
        if (_selectedTracks.Count == 0 || _selectedTracks.Contains(hovered)) return;

        var before = Project.Tracks.ToArray();
        var moving = before.Where(_selectedTracks.Contains).ToArray();
        var rest = before.Where(track => !_selectedTracks.Contains(track)).ToArray();
        if (moving.Length == 0) return;

        // Dropping below the block inserts after the hovered row, above it inserts before -
        // without that, a downward drag would put the block back where it started.
        var insert = IndexOf(rest, hovered) +
                     (IndexOf(before, hovered) > IndexOf(before, moving[0]) ? 1 : 0);
        var after = rest.Take(insert).Concat(moving).Concat(rest.Skip(insert)).ToArray();
        if (before.SequenceEqual(after)) return;

        Project.SetTrackOrder(after);
        _undoHistory.PushOrMergeMove(moving[0],
            () => Project.SetTrackOrder(before),
            () => Project.SetTrackOrder(after));
        Touch();
    }

    public bool RemoveTrack(ProjectTrack track)
    {
        var index = IndexOf(Project.Tracks, track);
        var cascadedPlacements = Project.Placements.Where(p => p.Track == track).ToArray();
        if (!Project.RemoveTrack(track)) return false;
        if (_selectedTracks.Remove(track)) OnSelectionChanged?.Invoke(SelectedTrack);
        RemoveFromPlacementSelection(cascadedPlacements);
        if (OpenedTrack == track) CloseTrack();

        // Clipboard placement entries referencing the removed track become dangling.
        if (_clipboard.Placements is { } clipPlacements)
        {
            var kept = clipPlacements.Where(e => e.Track != track).ToArray();
            if (kept.Length == 0) _clipboard.Clear();
            else if (kept.Length != clipPlacements.Count) _clipboard.SetPlacements(kept);
        }

        _undoHistory.Push(
            () =>
            {
                Project.AddTrack(track, index);
                foreach (var placement in cascadedPlacements) Project.AddPlacement(placement);
            },
            () => Project.RemoveTrack(track));
        Touch();
        return true;
    }

    /// <summary>
    ///     Imports a TDW sequence as one new track (+ its instruments + one
    ///     placement), as a single undo step. Throws whatever <see cref="SequenceImporter" />
    ///     throws on malformed/empty/runaway input - the caller is expected to alert and
    ///     leave the project untouched, which holds for free here since nothing is added
    ///     until the importer has already fully succeeded.
    /// </summary>
    public ImportResult ImportSequenceAsTrack(Sequence sequence, string name,
        IReadOnlyDictionary<string, Sound>? soundMap)
    {
        var result = SequenceImporter.AddAsTrack(Project, sequence, name, soundMap);
        var track = result.Track!;
        var placement = result.Placement!;
        var instruments = result.Instruments;

        var trackIndex = IndexOf(Project.Tracks, track);
        var instrumentIndices = instruments.Select(instrument => IndexOf(Project.Instruments, instrument)).ToArray();

        _undoHistory.Push(
            () =>
            {
                Project.RemovePlacement(placement);
                Project.RemoveTrack(track);
                foreach (var instrument in instruments) Project.RemoveInstrument(instrument);
            },
            () =>
            {
                Project.AddTrack(track, trackIndex);
                for (var i = 0; i < instruments.Count; i++) Project.AddInstrument(instruments[i], instrumentIndices[i]);
                Project.AddPlacement(placement);
            });
        Touch();
        return result;
    }

    public void OpenTrack(ProjectTrack track)
    {
        if (OpenedTrack == track) return;
        OpenedTrack = track;
        // A faithful track has no editable segments - its one default segment is an
        // artefact of the base class, and offering its bars/steps in the inspector would
        // be a form that changes nothing.
        SelectSegment(track is FaithfulTrack ? null : track.Segments[0]);
        SelectNote(null);
        SelectItem(null);
        ActiveInstrument = _lastInstrumentByTrack.TryGetValue(track, out var last)
            ? last
            : Instruments(track).FirstOrDefault();
        OnOpenedTrackChanged?.Invoke(track);
    }

    // ---------------------------------------------------------------- faithful items

    /// <summary>The opened track when it is a faithful one, else null - the faithful views' subject.</summary>
    public FaithfulTrack? OpenedFaithfulTrack => OpenedTrack as FaithfulTrack;

    /// <summary>
    ///     The selected slots of a faithful sequence - one under the Draw tool, any number
    ///     under Select. The faithful counterpart of <see cref="SelectedNotes" />.
    /// </summary>
    public IReadOnlyList<FaithfulItem> SelectedItems => _selectedItems;

    /// <summary>
    ///     The one selected slot, the inspector's subject there - null while nothing or more
    ///     than one is selected, exactly as <see cref="SelectedNote" />.
    /// </summary>
    public FaithfulItem? SelectedItem => _selectedItems.Count == 1 ? _selectedItems[0] : null;

    public event Action<FaithfulItem?>? OnItemSelectionChanged;

    public void SelectItem(FaithfulItem? item)
    {
        SetItemSelection(item is null ? [] : [item]);
    }

    public void SetItemSelection(IEnumerable<FaithfulItem> items)
    {
        var next = items.ToList();
        if (next.SequenceEqual(_selectedItems)) return;

        _selectedItems.Clear();
        _selectedItems.AddRange(next);
        OnItemSelectionChanged?.Invoke(SelectedItem);
    }

    /// <summary>Adds the item to the selection, or drops it when it is already in - Select's click.</summary>
    public void ToggleItemSelection(FaithfulItem item)
    {
        var next = _selectedItems.ToList();
        if (!next.Remove(item)) next.Add(item);
        SetItemSelection(next);
    }

    /// <summary>
    ///     Moves a slot to another index, one undo entry per landing spot. A drag fires this
    ///     once per boundary it crosses, so the entries merge on the item like a note drag's.
    /// </summary>
    public void MoveItem(FaithfulTrack track, int from, int to)
    {
        if (from < 0 || from >= track.Items.Count) return;
        to = Math.Clamp(to, 0, track.Items.Count - 1);
        if (from == to) return;

        var item = track.Items[from];
        track.Items.RemoveAt(from);
        track.Items.Insert(to, item);
        _undoHistory.PushOrMergeMove(item,
            () =>
            {
                track.Items.Remove(item);
                track.Items.Insert(from, item);
            },
            () =>
            {
                track.Items.Remove(item);
                track.Items.Insert(to, item);
            });
        Touch();
    }

    public void InsertItemAt(FaithfulTrack track, FaithfulItem item, int index)
    {
        index = Math.Clamp(index, 0, track.Items.Count);
        track.Items.Insert(index, item);
        _undoHistory.Push(
            () => track.Items.Remove(item),
            () => track.Items.Insert(index, item));
        Touch();
    }

    public void AppendItem(FaithfulTrack track, FaithfulItem item)
    {
        InsertItemAt(track, item, track.Items.Count);
    }

    public void RemoveItemAt(FaithfulTrack track, int index)
    {
        if (index < 0 || index >= track.Items.Count) return;

        var item = track.Items[index];
        track.Items.RemoveAt(index);
        if (_selectedItems.Contains(item)) SetItemSelection(_selectedItems.Where(selected => selected != item));
        _undoHistory.Push(
            () => track.Items.Insert(index, item),
            () => track.Items.RemoveAt(index));
        Touch();
    }

    /// <summary>
    ///     Applies a scroll adjustment to one item. A run of scrolls on the same item inside
    ///     one gesture merges into a single undo entry, same as a note drag - the item's
    ///     identity survives, only its fields move, so undo restores those.
    /// </summary>
    public void AdjustItem(FaithfulItem item, Action adjust)
    {
        var before = item.Duplicate();
        adjust();
        var after = item.Duplicate();

        _undoHistory.PushOrMergeMove(item, () => Restore(item, before), () => Restore(item, after));
        Touch();
    }

    private static void Restore(FaithfulItem target, FaithfulItem snapshot)
    {
        if (target.Note is { } note && snapshot.Note is { } savedNote)
        {
            note.Instrument = savedNote.Instrument;
            note.Value = savedNote.Value;
            note.Volume = savedNote.Volume;
            note.Pan = savedNote.Pan;
            note.Offset = savedNote.Offset;
        }

        if (target.Action is not { } action || snapshot.Action is not { } savedAction) return;
        action.Value = savedAction.Value;
        action.WorkingValue = savedAction.WorkingValue;
        action.ValueScale = savedAction.ValueScale;
    }

    /// <summary>The instruments a track plays, in track order - both kinds hold them differently.</summary>
    private static IEnumerable<Instrument> Instruments(ProjectTrack track)
    {
        return track is FaithfulTrack faithful
            ? faithful.Items.Select(item => item.Note?.Instrument).OfType<Instrument>()
            : track.Segments.SelectMany(segment => segment.Notes).Select(note => note.Instrument);
    }

    public void CloseTrack()
    {
        if (OpenedTrack == null) return;
        OpenedTrack = null;
        SelectSegment(null);
        SelectNote(null);
        SelectItem(null);
        CopiedModifiers = null;
        OnOpenedTrackChanged?.Invoke(null);
    }

    public void SelectSegment(TrackSegment? segment)
    {
        if (SelectedSegment == segment) return;
        SelectedSegment = segment;
        OnSegmentSelectionChanged?.Invoke(segment);
    }

    /// <summary>
    ///     Replaces the note selection with a single note, or clears it when null.
    ///     Every existing call site (paint, drag-press, cascades) keeps working unchanged.
    /// </summary>
    public void SelectNote(Note? note)
    {
        SetNoteSelection(note != null ? [note] : []);
    }

    /// <summary>
    ///     Replaces the whole note selection. Fires <see cref="OnNoteSelectionChanged" />
    ///     once, even for multi-note selections (existing subscribers read the derived
    ///     <see cref="SelectedNote" />, which is non-null only for a single note).
    /// </summary>
    public void SetNoteSelection(IEnumerable<Note> notes)
    {
        var next = notes as IReadOnlyList<Note> ?? [.. notes];
        if (SequenceRefEqual(_selectedNotes, next)) return;
        _selectedNotes.Clear();
        _selectedNotes.AddRange(next);
        AfterNoteSelectionChanged();
    }

    /// <summary>
    ///     Appends notes not already selected. No-op for ones already present
    ///     (append semantics, not toggle).
    /// </summary>
    public void AddToNoteSelection(IEnumerable<Note> notes)
    {
        var added = false;
        foreach (var note in notes)
            if (!_selectedNotes.Contains(note))
            {
                _selectedNotes.Add(note);
                added = true;
            }

        if (added) AfterNoteSelectionChanged();
    }

    /// <summary>Removes notes from the selection. No-op for ones not present.</summary>
    public void RemoveFromNoteSelection(IEnumerable<Note> notes)
    {
        var removed = false;
        foreach (var note in notes.ToArray())
            removed |= _selectedNotes.Remove(note);

        if (removed) AfterNoteSelectionChanged();
    }

    /// <summary>
    ///     Replaces the placement selection with a single placement, or clears it
    ///     when null. Every existing call site keeps working unchanged.
    /// </summary>
    public void SelectPlacement(TrackPlacement? placement)
    {
        SetPlacementSelection(placement != null ? [placement] : []);
    }

    /// <summary>
    ///     Replaces the whole placement selection. Fires <see cref="OnPlacementSelectionChanged" />
    ///     once, even for multi-placement selections.
    /// </summary>
    public void SetPlacementSelection(IEnumerable<TrackPlacement> placements)
    {
        var next = placements as IReadOnlyList<TrackPlacement> ?? [.. placements];
        if (SequenceRefEqual(_selectedPlacements, next)) return;
        _selectedPlacements.Clear();
        _selectedPlacements.AddRange(next);
        OnPlacementSelectionChanged?.Invoke(SelectedPlacement);
    }

    /// <summary>Appends placements not already selected. No-op for ones already present.</summary>
    public void AddToPlacementSelection(IEnumerable<TrackPlacement> placements)
    {
        var added = false;
        foreach (var placement in placements)
            if (!_selectedPlacements.Contains(placement))
            {
                _selectedPlacements.Add(placement);
                added = true;
            }

        if (added) OnPlacementSelectionChanged?.Invoke(SelectedPlacement);
    }

    /// <summary>Removes placements from the selection. No-op for ones not present.</summary>
    public void RemoveFromPlacementSelection(IEnumerable<TrackPlacement> placements)
    {
        var removed = false;
        foreach (var placement in placements.ToArray())
            removed |= _selectedPlacements.Remove(placement);

        if (removed) OnPlacementSelectionChanged?.Invoke(SelectedPlacement);
    }

    /// <summary>
    ///     Selects every note of the opened track (all segments) when a track is
    ///     open, otherwise every placement on the arrangement.
    /// </summary>
    public void SelectAll()
    {
        if (OpenedFaithfulTrack is { } faithful) SetItemSelection(faithful.Items);
        else if (OpenedTrack is { } track) SetNoteSelection(track.Segments.SelectMany(s => s.Notes));
        else SetPlacementSelection(Project.Placements);
    }

    /// <summary>Clears the note, placement and faithful-item selections.</summary>
    public void ClearSelection()
    {
        SetNoteSelection([]);
        SetPlacementSelection([]);
        SetItemSelection([]);
    }

    /// <summary>
    ///     Copies the current selection (notes when a track is open, otherwise
    ///     placements) into the internal clipboard. No-op on an empty selection.
    /// </summary>
    public void CopySelection()
    {
        if (OpenedFaithfulTrack is { } faithful)
        {
            if (_selectedItems.Count == 0) return;
            // In sequence order, not click order: a paste has to read like what was copied.
            _clipboard.SetItems(faithful.Items.Where(_selectedItems.Contains).Select(item => item.Duplicate()));
            return;
        }

        if (OpenedTrack is { } track)
        {
            if (_selectedNotes.Count == 0) return;
            _clipboard.SetNotes(_selectedNotes
                .Select(note => new EditorClipboard.NoteEntry(GlobalStepOf(track, note), note.Duplicate())));
        }
        else
        {
            if (_selectedPlacements.Count == 0) return;
            _clipboard.SetPlacements(_selectedPlacements
                .Select(p => new EditorClipboard.PlacementEntry(p.Track, p.Channel, p.StartQuarterNotes)));
        }
    }

    /// <summary>
    ///     Pastes the clipboard payload in place: notes into the opened track (mapped
    ///     through <see cref="ProjectTrack.SegmentAtGlobalStep" />, dropping any past the
    ///     track's end). One clone per copied note, always - a clone landing on an identical
    ///     existing note stacks on top of it rather than being dropped or nudged aside, so
    ///     the paste is one-to-one with the copy and can be dragged off the originals as a
    ///     whole; placements as fresh clips on the arrangement. Cross-editor
    ///     mismatches (notes payload while the arrangement is shown, or vice versa) are a
    ///     silent no-op. The pasted clones become the new selection, and the whole paste is
    ///     one undo entry.
    /// </summary>
    public void Paste()
    {
        if (_clipboard.Items is { } itemEntries)
        {
            if (OpenedFaithfulTrack is not { } faithful) return; // cross-editor mismatch

            // After the last selected slot, or at the end when nothing is selected - the same
            // "where you were working" spot the note paste lands on.
            var at = _selectedItems.Count == 0
                ? faithful.Items.Count
                : _selectedItems.Max(faithful.Items.IndexOf) + 1;
            var pasted = itemEntries.Select(item => item.Duplicate()).ToArray();
            faithful.Items.InsertRange(at, pasted);

            SetItemSelection(pasted);
            _undoHistory.Push(
                () => faithful.Items.RemoveRange(at, pasted.Length),
                () => faithful.Items.InsertRange(at, pasted));
            Touch();
            return;
        }

        if (_clipboard.Notes is { } noteEntries)
        {
            if (OpenedTrack is not { } track) return; // cross-editor mismatch

            var pasted = new List<(TrackSegment Segment, Note Note)>();
            foreach (var entry in noteEntries)
            {
                if (track.SegmentAtGlobalStep(entry.GlobalStep) is not { } mapped) continue;
                var (segment, localStep) = mapped;
                var clone = entry.Snapshot.Duplicate();
                clone.Step = localStep;

                segment.Notes.Add(clone);
                pasted.Add((segment, clone));
            }

            if (pasted.Count == 0) return;
            SetNoteSelection(pasted.Select(p => p.Note));
            _undoHistory.Push(
                () =>
                {
                    foreach (var (segment, note) in pasted) segment.Notes.Remove(note);
                },
                () =>
                {
                    foreach (var (segment, note) in pasted) segment.Notes.Add(note);
                });
            Touch();
        }
        else if (_clipboard.Placements is { } placementEntries)
        {
            if (OpenedTrack != null) return; // cross-editor mismatch
            var pasted = placementEntries
                .Select(entry => Project.Place(entry.Track, entry.Channel, entry.StartQuarterNotes))
                .ToArray();

            SetPlacementSelection(pasted);
            _undoHistory.Push(
                () =>
                {
                    foreach (var placement in pasted) Project.RemovePlacement(placement);
                },
                () =>
                {
                    foreach (var placement in pasted) Project.AddPlacement(placement);
                });
            Touch();
        }
    }

    /// <summary>Copies the selection, then deletes it - one undo entry (the delete's).</summary>
    public void CutSelection()
    {
        CopySelection();
        DeleteSelection();
    }

    /// <summary>
    ///     Deletes the whole selection (whichever list is populated) as one composite
    ///     undo entry - mirrors <see cref="DeleteInstrumentEverywhere" />'s snapshot-then-
    ///     remove-then-push pattern.
    /// </summary>
    public void DeleteSelection()
    {
        if (_selectedItems.Count > 0 && OpenedFaithfulTrack is { } faithful)
        {
            // Index-and-item pairs, so undo puts each slot back where it was.
            var snapshot = faithful.Items
                .Select((item, index) => (Index: index, Item: item))
                .Where(pair => _selectedItems.Contains(pair.Item))
                .ToArray();
            foreach (var (_, item) in snapshot) faithful.Items.Remove(item);
            ClearSelection();

            _undoHistory.Push(
                () =>
                {
                    foreach (var (index, item) in snapshot) faithful.Items.Insert(index, item);
                },
                () =>
                {
                    foreach (var (_, item) in snapshot) faithful.Items.Remove(item);
                });
            Touch();
            return;
        }

        if (_selectedNotes.Count > 0 && OpenedTrack is { } track)
        {
            var snapshot = _selectedNotes
                .Select(note => (Segment: track.Segments.FirstOrDefault(s => s.Notes.Contains(note)), Note: note))
                .Where(pair => pair.Segment != null)
                .Select(pair => (pair.Segment!, pair.Note))
                .ToArray();
            foreach (var (segment, note) in snapshot) segment.Notes.Remove(note);
            ClearSelection();

            _undoHistory.Push(
                () =>
                {
                    foreach (var (segment, note) in snapshot) segment.Notes.Add(note);
                },
                () =>
                {
                    foreach (var (segment, note) in snapshot) segment.Notes.Remove(note);
                });
            Touch();
        }
        else if (_selectedPlacements.Count > 0)
        {
            var snapshot = _selectedPlacements.ToArray();
            foreach (var placement in snapshot) Project.RemovePlacement(placement);
            ClearSelection();

            _undoHistory.Push(
                () =>
                {
                    foreach (var placement in snapshot) Project.AddPlacement(placement);
                },
                () =>
                {
                    foreach (var placement in snapshot) Project.RemovePlacement(placement);
                });
            Touch();
        }
    }

    private void AfterNoteSelectionChanged()
    {
        if (_selectedNotes.Count == 1) CopiedModifiers = _selectedNotes[0];
        OnNoteSelectionChanged?.Invoke(SelectedNote);
    }

    private static int GlobalStepOf(ProjectTrack track, Note note)
    {
        var segment = track.Segments.FirstOrDefault(s => s.Notes.Contains(note));
        return segment != null ? track.GlobalStepOf(segment, note) : note.Step;
    }

    private static bool SequenceRefEqual<T>(List<T> current, IReadOnlyList<T> next) where T : class
    {
        if (current.Count != next.Count) return false;
        for (var i = 0; i < current.Count; i++)
            if (!ReferenceEquals(current[i], next[i]))
                return false;
        return true;
    }

    public Note AddNote(TrackSegment segment, int step, Instrument instrument, double value, bool isCut = false)
    {
        // A cut note always has default Volume/Pan/Offset/Automation (see Note.IsCut) -
        // copied modifiers from a previous pick never apply to it.
        var modifiers = isCut ? null : CopiedModifiers;
        var note = new Note
        {
            Step = step,
            Instrument = instrument,
            Value = value,
            Volume = modifiers?.Volume,
            Pan = modifiers?.Pan ?? 0,
            Offset = modifiers?.Offset ?? 0,
            Automation = modifiers?.Automation?.Clone(),
            IsCut = isCut
        };
        segment.Notes.Add(note);
        _undoHistory.Push(
            () => segment.Notes.Remove(note),
            () => segment.Notes.Add(note));
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

    /// <summary>
    ///     Replaces an instrument's sounds. The instances are cloned - the editor's
    ///     picker keeps mutating its own as the user scrolls them.
    /// </summary>
    public void SetInstrumentSounds(Instrument instrument, IEnumerable<InstrumentSound> sounds)
    {
        instrument.Sounds.Clear();
        instrument.Sounds.AddRange(sounds.Select(sound => sound.Clone()));

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

        removedNotes.Reverse();

        RemoveFromNoteSelection(removedNotes.Select(r => r.Note));

        var instrumentIndex = IndexOf(Project.Instruments, instrument);
        _undoHistory.Push(
            () =>
            {
                Project.AddInstrument(instrument, instrumentIndex);
                foreach (var (segment, note, index) in removedNotes)
                    segment.Notes.Insert(Math.Clamp(index, 0, segment.Notes.Count), note);
            },
            () =>
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
            () =>
            {
                if (to != prevSegment)
                {
                    to.Notes.Remove(note);
                    prevSegment.Notes.Add(note);
                }

                note.Step = prevStep;
                note.Value = prevValue;
            },
            () =>
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

    /// <summary>
    ///     Moves every given note to its target (segment, step, value) together, as one
    ///     shared drag gesture - dragging any note that's part of a (possibly multi-note)
    ///     selection moves the whole group. Every drag frame calls this again with the
    ///     group's latest targets; <see cref="BeginGesture" /> + <see cref="UndoHistory.PushOrMergeMove" />
    ///     collapse the whole sequence into ONE undo entry, keyed on the first note in the
    ///     list so every frame's call is recognized as the same gesture (matching
    ///     <see cref="MoveNote" />'s single-note merge, generalized to a group).
    /// </summary>
    public void MoveSelectedNotes(ProjectTrack track,
        IReadOnlyList<(Note Note, TrackSegment Segment, int Step, double Value)> targets)
    {
        if (targets.Count == 0) return;

        var before = targets
            .Select(t => (t.Note, Segment: FindSegment(track, t.Note), t.Note.Step, t.Note.Value))
            .ToArray();
        var changed = false;
        for (var i = 0; i < targets.Count; i++)
        {
            var (note, segment, step, value) = targets[i];
            if (before[i].Segment != segment || note.Step != step || note.Value != value) changed = true;
        }

        if (!changed) return;

        Apply(track, targets);

        _undoHistory.PushOrMergeMove(targets[0].Note,
            () => Apply(track, before),
            () => Apply(track, targets));
        Touch();
    }

    private static TrackSegment FindSegment(ProjectTrack track, Note note)
    {
        return track.Segments.First(s => s.Notes.Contains(note));
    }

    /// <summary>
    ///     Places every note at its target (segment, step, value), removing it from
    ///     whichever segment currently holds it first - robust regardless of which frame's
    ///     closure (undo/redo) runs, since it never assumes the note's current location.
    /// </summary>
    private static void Apply(ProjectTrack track,
        IReadOnlyList<(Note Note, TrackSegment Segment, int Step, double Value)> targets)
    {
        foreach (var (note, segment, step, value) in targets)
        {
            foreach (var s in track.Segments) s.Notes.Remove(note);
            segment.Notes.Add(note);
            note.Step = step;
            note.Value = value;
        }
    }

    public bool RemoveNote(TrackSegment segment, Note note)
    {
        if (!segment.Notes.Remove(note)) return false;
        RemoveFromNoteSelection([note]);
        _undoHistory.Push(
            () => segment.Notes.Add(note),
            () => segment.Notes.Remove(note));
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
        RemoveFromNoteSelection(segment.Notes);
        if (SelectedSegment == segment) SelectSegment(track.Segments[0]);

        _undoHistory.Push(
            () => track.AddSegment(segment, index),
            () => track.RemoveSegment(segment));
        Touch();
        return true;
    }

    public TrackPlacement PlaceTrack(ProjectTrack track, int channel, double startQuarterNotes)
    {
        var placement = Project.Place(track, channel, startQuarterNotes);
        _undoHistory.Push(
            () => Project.RemovePlacement(placement),
            () => Project.AddPlacement(placement));
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
            () =>
            {
                placement.Channel = prevChannel;
                placement.StartQuarterNotes = prevStart;
            },
            () =>
            {
                placement.Channel = channel;
                placement.StartQuarterNotes = startQuarterNotes;
            });
        Touch();
    }

    /// <summary>
    ///     Arrow-key nudge of the selected notes: whole steps along the track and values
    ///     up/down, clamped the same way a drag is (<paramref name="maxValue" /> is the
    ///     view's value range). One <see cref="BeginGesture" /> per call, so a keystroke is
    ///     its own undo entry rather than merging into the drag before it.
    /// </summary>
    public void NudgeNotes(int stepDelta, double valueDelta, double maxValue)
    {
        if (OpenedTrack is not { } track || _selectedNotes.Count == 0) return;

        var maxGlobalStep = Math.Max(0, track.Segments.Sum(segment => segment.StepCount) - 1);
        var targets = new List<(Note Note, TrackSegment Segment, int Step, double Value)>(_selectedNotes.Count);
        foreach (var note in _selectedNotes)
        {
            var globalStep = Math.Clamp(
                track.GlobalStepOf(FindSegment(track, note), note.Step) + stepDelta, 0, maxGlobalStep);
            if (track.SegmentAtGlobalStep(globalStep) is not { } mapped) continue;

            targets.Add((note, mapped.Segment, mapped.LocalStep,
                Math.Clamp(note.Value + valueDelta, -maxValue, maxValue)));
        }

        BeginGesture();
        MoveSelectedNotes(track, targets);
    }

    /// <summary>
    ///     The arrangement's counterpart to <see cref="NudgeNotes" />: the selected clips
    ///     move by <paramref name="startDelta" /> quarter notes and whole channels, clamped
    ///     to the start of the timeline and to <paramref name="maxChannel" />.
    /// </summary>
    public void NudgePlacements(double startDelta, int channelDelta, int maxChannel)
    {
        if (_selectedPlacements.Count == 0) return;

        BeginGesture();
        MoveSelectedPlacements(_selectedPlacements.Select(placement => (
            placement,
            Math.Clamp(placement.Channel + channelDelta, 0, maxChannel),
            Math.Max(0, placement.StartQuarterNotes + startDelta))).ToArray());
    }

    /// <summary>
    ///     Moves every given placement to its target (channel, start) together - the
    ///     arrangement's counterpart to <see cref="MoveSelectedNotes" />, keyed on the first
    ///     placement so a run of calls inside one gesture collapses into ONE undo entry
    ///     instead of one per clip.
    /// </summary>
    private void MoveSelectedPlacements(IReadOnlyList<(TrackPlacement Placement, int Channel, double Start)> targets)
    {
        if (targets.Count == 0) return;

        var before = targets
            .Select(t => (t.Placement, t.Placement.Channel, Start: t.Placement.StartQuarterNotes))
            .ToArray();
        var changed = false;
        for (var i = 0; i < targets.Count; i++)
            if (before[i].Channel != targets[i].Channel || before[i].Start != targets[i].Start)
                changed = true;

        if (!changed) return;

        ApplyPlacements(targets);

        _undoHistory.PushOrMergeMove(targets[0].Placement,
            () => ApplyPlacements(before),
            () => ApplyPlacements(targets));
        Touch();
    }

    private static void ApplyPlacements(IReadOnlyList<(TrackPlacement Placement, int Channel, double Start)> targets)
    {
        foreach (var (placement, channel, start) in targets)
        {
            placement.Channel = channel;
            placement.StartQuarterNotes = start;
        }
    }

    public bool RemovePlacement(TrackPlacement placement)
    {
        if (!Project.RemovePlacement(placement)) return false;
        RemoveFromPlacementSelection([placement]);
        _undoHistory.Push(
            () => Project.AddPlacement(placement),
            () => Project.RemovePlacement(placement));
        Touch();
        return true;
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

    /// <summary>
    ///     Recolors a track's clips on the arrangement. The index addresses the view's
    ///     palette (<c>ArrangementView.ClipPalette</c>); null restores the default
    ///     clip fill. Not undoable, like <see cref="RenameTrack" /> - it changes nothing
    ///     the project sounds like.
    /// </summary>
    public void SetTrackColor(ProjectTrack track, int? colorIndex)
    {
        if (track.ColorIndex == colorIndex) return;
        track.ColorIndex = colorIndex;
        Touch();
    }

    /// <summary>Replaces the whole track selection with one track (or clears it).</summary>
    public void SelectTrack(ProjectTrack? track)
    {
        SetTrackSelection(track != null ? [track] : []);
    }

    /// <summary>
    ///     Replaces the whole track selection. Fires <see cref="OnSelectionChanged" /> once,
    ///     with the derived single value (see <see cref="SelectedTrack" />).
    /// </summary>
    public void SetTrackSelection(IEnumerable<ProjectTrack> tracks)
    {
        var next = tracks as IReadOnlyList<ProjectTrack> ?? [.. tracks];
        if (SequenceRefEqual(_selectedTracks, next)) return;
        _selectedTracks.Clear();
        _selectedTracks.AddRange(next);
        OnSelectionChanged?.Invoke(SelectedTrack);
    }

    /// <summary>Ctrl/Cmd-click: adds the track to the selection, or removes it if it is already in.</summary>
    public void ToggleTrackSelection(ProjectTrack track)
    {
        if (!_selectedTracks.Remove(track)) _selectedTracks.Add(track);
        OnSelectionChanged?.Invoke(SelectedTrack);
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

    /// <summary>
    ///     Marks the start of a new drag gesture, so a run of MoveNote/MovePlacement
    ///     calls on the same object (one drag, many frames) collapses into a single undo step.
    /// </summary>
    public void BeginGesture()
    {
        _undoHistory.BeginGesture();
    }

    public void Undo()
    {
        if (!_undoHistory.Undo()) return;
        ClearSelection();
        Touch();
    }

    public void Redo()
    {
        if (!_undoHistory.Redo()) return;
        ClearSelection();
        Touch();
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
using ThirtyDollarConverter.Editor;

namespace EditorScene.State;

/// <summary>
///     Cut/copy/paste/delete across all three editors. Which selection they act on follows
///     what is open: faithful items, then notes, then the arrangement's clips.
/// </summary>
public partial class EditorState
{
    private readonly EditorClipboard _clipboard = new();

    /// <summary>
    ///     Copies the current selection (notes when a track is open, otherwise
    ///     placements) into the internal clipboard. No-op on an empty selection.
    /// </summary>
    public void CopySelection()
    {
        if (OpenedFaithfulTrack is { } faithful)
        {
            if (_items.Count == 0) return;
            // In sequence order, not click order: a paste has to read like what was copied.
            _clipboard.SetItems(faithful.Items.Where(_items.Contains).Select(item => item.Duplicate()));
            return;
        }

        if (OpenedTrack is { } track)
        {
            if (_notes.Count == 0) return;
            _clipboard.SetNotes(_notes.Items
                .Select(note => new EditorClipboard.NoteEntry(GlobalStepOf(track, note), note.Duplicate())));
        }
        else
        {
            if (_placements.Count == 0) return;
            _clipboard.SetPlacements(_placements.Items
                .Select(p => new EditorClipboard.PlacementEntry(p.Track, p.Channel, p.StartQuarterNotes)));
        }
    }

    /// <summary>
    ///     Pastes the clipboard payload in place: notes into the opened track (mapped through
    ///     <see cref="ProjectTrack.SegmentAtGlobalStep" />, dropping any past the track's end),
    ///     placements as fresh clips on the arrangement. Always one clone per copied note - a
    ///     clone landing on an identical existing note stacks on top of it, so the paste stays
    ///     one-to-one with the copy and can be dragged off the originals as a whole.
    ///     Cross-editor mismatches (a notes payload while the arrangement is shown, or vice
    ///     versa) are a silent no-op. The pasted clones become the new selection, and the
    ///     whole paste is one undo entry.
    /// </summary>
    public void Paste()
    {
        if (_clipboard.Items is { } itemEntries)
        {
            if (OpenedFaithfulTrack is not { } faithful) return; // cross-editor mismatch

            // After the last selected slot, or at the end when nothing is selected - the same
            // "where you were working" spot the note paste lands on.
            var at = _items.Count == 0
                ? faithful.Items.Count
                : _items.Items.Max(faithful.Items.IndexOf) + 1;
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
    ///     Deletes the whole selection - whichever of the item, note and placement lists is
    ///     populated - as one composite undo entry.
    /// </summary>
    public void DeleteSelection()
    {
        if (_items.Count > 0 && OpenedFaithfulTrack is { } faithful)
        {
            // Index-and-item pairs, so undo puts each slot back where it was.
            var snapshot = faithful.Items
                .Select((item, index) => (Index: index, Item: item))
                .Where(pair => _items.Contains(pair.Item))
                .ToArray();
            foreach (var (_, item) in snapshot) faithful.Items.Remove(item);
            ClearSelection();

            _undoHistory.Push(
                () =>
                {
                    foreach (var (index, item) in snapshot) faithful.Items.Insert(index, item);
                    SetItemSelection(snapshot.Select(pair => pair.Item).ToArray());
                },
                () =>
                {
                    foreach (var (_, item) in snapshot) faithful.Items.Remove(item);
                });
            Touch();
            return;
        }

        if (_notes.Count > 0 && OpenedTrack is { } track)
        {
            var snapshot = _notes.Items
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
                    SetNoteSelection(snapshot.Select(pair => pair.Note).ToArray());
                },
                () =>
                {
                    foreach (var (segment, note) in snapshot) segment.Notes.Remove(note);
                });
            Touch();
        }
        else if (_placements.Count > 0)
        {
            var snapshot = _placements.Items.ToArray();
            foreach (var placement in snapshot) Project.RemovePlacement(placement);
            ClearSelection();

            _undoHistory.Push(
                () =>
                {
                    foreach (var placement in snapshot) Project.AddPlacement(placement);
                    SetPlacementSelection(snapshot);
                },
                () =>
                {
                    foreach (var placement in snapshot) Project.RemovePlacement(placement);
                });
            Touch();
        }
    }

    /// <summary>
    ///     Drops clipboard placement entries whose track no longer qualifies - a removed track,
    ///     or one an undo took back out of the project. Both leave entries a paste would hand
    ///     to Place, which rejects foreign tracks.
    /// </summary>
    private void PruneClipboardPlacements(Func<ProjectTrack, bool> alive)
    {
        if (_clipboard.Placements is not { } entries) return;

        var kept = entries.Where(entry => alive(entry.Track)).ToArray();
        if (kept.Length == 0) _clipboard.Clear();
        else if (kept.Length != entries.Count) _clipboard.SetPlacements(kept);
    }
}

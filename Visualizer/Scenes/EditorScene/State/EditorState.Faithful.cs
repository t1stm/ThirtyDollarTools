using ThirtyDollarConverter.Editor;

namespace EditorScene.State;

/// <summary>
///     The faithful side: a linear sequence of slots (<see cref="FaithfulItem" />) rather than
///     a bar/beat grid. Slots are inserted, replaced, slid along the sequence and scroll-adjusted.
/// </summary>
public partial class EditorState
{
    /// <summary>The opened track when it is a faithful one, else null - the faithful views' subject.</summary>
    public FaithfulTrack? OpenedFaithfulTrack => OpenedTrack as FaithfulTrack;

    /// <summary>
    ///     Slides every given slot the same distance along the sequence - dragging one slot
    ///     of a selection drags all of it, as <see cref="MoveSelectedNotes" /> does in the
    ///     piano roll. A drag fires this once per boundary it crosses; the entries merge on
    ///     the lowest slot, so the whole drag is one undo entry. The distance is clamped so
    ///     the block stops at either end rather than the leading slot running off it.
    /// </summary>
    public void MoveItems(FaithfulTrack track, IReadOnlyList<FaithfulItem> items, int delta)
    {
        var spots = items.Select(item => (Item: item, Index: track.Items.IndexOf(item)))
            .Where(spot => spot.Index >= 0).OrderBy(spot => spot.Index).ToArray();
        if (spots.Length == 0) return;

        delta = Math.Clamp(delta, -spots[0].Index, track.Items.Count - 1 - spots[^1].Index);
        if (delta == 0) return;

        var moved = spots.Select(spot => (spot.Item, Index: spot.Index + delta)).ToArray();
        Place(track, moved);
        // Both closures are absolute positions, not a relative slide: a merged drag keeps the
        // first frame's undo and the last frame's redo, and only absolutes survive that.
        _undoHistory.PushOrMergeMove(spots[0].Item, () => Place(track, spots), () => Place(track, moved));
        Touch();
    }

    public void InsertItemAt(FaithfulTrack track, FaithfulItem item, int index)
    {
        index = Math.Clamp(index, 0, track.Items.Count);
        track.Items.Insert(index, item);
        _undoHistory.PushInsert(track.Items, item, index);
        Touch();
    }

    public void AppendItem(FaithfulTrack track, FaithfulItem item)
    {
        InsertItemAt(track, item, track.Items.Count);
    }

    /// <summary>
    ///     Swaps one slot for another, one undo entry. What the right-click value dialog
    ///     commits: the text it hands back can be any event at all, not just a new value on
    ///     the one that was there, so the item is replaced rather than adjusted in place.
    /// </summary>
    public void ReplaceItemAt(FaithfulTrack track, int index, FaithfulItem item)
    {
        if (index < 0 || index >= track.Items.Count) return;

        var old = track.Items[index];
        track.Items[index] = item;
        _items.Replace(old, item);

        _undoHistory.Push(
            () => track.Items[index] = old,
            () => track.Items[index] = item);
        Touch();
    }

    public void RemoveItemAt(FaithfulTrack track, int index)
    {
        if (index < 0 || index >= track.Items.Count) return;

        var item = track.Items[index];
        track.Items.RemoveAt(index);
        _items.Remove([item]);
        _undoHistory.PushRemove(track.Items, item, index);
        Touch();
    }

    /// <summary>
    ///     Applies a scroll adjustment to every given item - one item under Draw, the whole
    ///     selection under Select - as a single undo entry. A run of scrolls on the same items
    ///     inside one gesture merges into that entry; the items keep their identity and only
    ///     their fields move, so undo restores the fields.
    /// </summary>
    public void AdjustItems(IReadOnlyList<FaithfulItem> items, Action<FaithfulItem> adjust)
    {
        // The caller may hand the live selection over; the undo closures outlive it.
        var targets = items.ToArray();
        if (targets.Length == 0) return;

        var before = targets.Select(item => item.Duplicate()).ToArray();
        foreach (var item in targets) adjust(item);
        var after = targets.Select(item => item.Duplicate()).ToArray();

        _undoHistory.PushOrMergeMove(targets[0],
            () => RestoreAll(targets, before),
            () => RestoreAll(targets, after));
        Touch();
    }

    /// <summary>
    ///     Puts each item back at its recorded index: pulled out first, then reinserted in
    ///     index order, so every insert lands in a list whose earlier half is already final.
    /// </summary>
    private static void Place(FaithfulTrack track, IReadOnlyList<(FaithfulItem Item, int Index)> spots)
    {
        foreach (var (item, _) in spots) track.Items.Remove(item);
        foreach (var (item, index) in spots) track.Items.Insert(index, item);
    }

    private static void RestoreAll(IReadOnlyList<FaithfulItem> targets, IReadOnlyList<FaithfulItem> snapshots)
    {
        for (var i = 0; i < targets.Count; i++) Restore(targets[i], snapshots[i]);
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
}

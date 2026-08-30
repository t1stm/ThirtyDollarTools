using ThirtyDollarConverter.Editor;

namespace EditorScene;

/// <summary>
///     Session-only internal clipboard for the editor's Copy/Paste/Cut, separate from the
///     OS clipboard. Holds one typed payload at a time: setting notes clears any copied
///     placements and items, and vice versa.
/// </summary>
public sealed class EditorClipboard
{
    public IReadOnlyList<NoteEntry>? Notes { get; private set; }
    public IReadOnlyList<PlacementEntry>? Placements { get; private set; }

    /// <summary>Copied faithful slots, in sequence order; a slot's position is its index.</summary>
    public IReadOnlyList<FaithfulItem>? Items { get; private set; }

    public void SetNotes(IEnumerable<NoteEntry> entries)
    {
        Notes = [.. entries];
        Placements = null;
        Items = null;
    }

    public void SetItems(IEnumerable<FaithfulItem> items)
    {
        Items = [.. items];
        Notes = null;
        Placements = null;
    }

    public void SetPlacements(IEnumerable<PlacementEntry> entries)
    {
        Placements = [.. entries];
        Notes = null;
        Items = null;
    }

    public void Clear()
    {
        Notes = null;
        Placements = null;
        Items = null;
    }

    /// <summary>
    ///     A copied note with its track-absolute step (see <see cref="ProjectTrack.GlobalStepOf" />),
    ///     so paste can remap it onto a track with a different segment layout.
    /// </summary>
    public sealed record NoteEntry(int GlobalStep, Note Snapshot);

    public sealed record PlacementEntry(ProjectTrack Track, int Channel, double StartQuarterNotes);
}
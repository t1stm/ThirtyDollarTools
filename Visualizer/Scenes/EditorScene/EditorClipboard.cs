using ThirtyDollarConverter.Editor;

namespace EditorScene;

/// <summary>
///     Session-only internal clipboard for the editor's Copy/Paste/Cut - plain data,
///     one typed slot (copying notes overwrites a placement payload and vice versa).
///     Not the OS clipboard: nothing text-serializable exists yet for a selection.
/// </summary>
public sealed class EditorClipboard
{
    public IReadOnlyList<NoteEntry>? Notes { get; private set; }
    public IReadOnlyList<PlacementEntry>? Placements { get; private set; }

    public void SetNotes(IEnumerable<NoteEntry> entries)
    {
        Notes = entries.ToArray();
        Placements = null;
    }

    public void SetPlacements(IEnumerable<PlacementEntry> entries)
    {
        Placements = entries.ToArray();
        Notes = null;
    }

    public void Clear()
    {
        Notes = null;
        Placements = null;
    }

    /// <summary>
    ///     A copied note with its track-absolute step (see <see cref="ProjectTrack.GlobalStepOf" />),
    ///     so paste can remap it onto a track with a different segment layout.
    /// </summary>
    public sealed record NoteEntry(int GlobalStep, Note Snapshot);

    public sealed record PlacementEntry(ProjectTrack Track, int Channel, double StartQuarterNotes);
}
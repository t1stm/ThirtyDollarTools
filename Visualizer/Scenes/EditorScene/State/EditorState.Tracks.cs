using JetBrains.Annotations;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.State;

/// <summary>
///     Tracks: creating, duplicating, reordering, removing, converting between the two kinds,
///     importing one from a TDW sequence, and opening one in the note editor.
/// </summary>
public partial class EditorState
{
    /// <summary>The track open in the note editor; null means the arrangement is shown.</summary>
    public ProjectTrack? OpenedTrack { get; private set; }

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
        if (_tracks.Count == 0 || _tracks.Contains(hovered)) return;

        var before = Project.Tracks.ToArray();
        var moving = before.Where(_tracks.Contains).ToArray();
        var rest = before.Where(track => !_tracks.Contains(track)).ToArray();
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
        var cascadedPlacements = Project.Placements.Where(placement => placement.Track == track).ToArray();
        if (!Project.RemoveTrack(track)) return false;
        _tracks.Remove([track]);
        RemoveFromPlacementSelection(cascadedPlacements);
        if (OpenedTrack == track) CloseTrack();

        // Clipboard placement entries referencing the removed track become dangling.
        PruneClipboardPlacements(entryTrack => entryTrack != track);

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
    ///     Imports a TDW sequence as one new track (+ its instruments + one placement), as a
    ///     single undo step. Throws whatever <see cref="SequenceImporter" /> throws on
    ///     malformed/empty/runaway input; nothing is added until the importer has fully
    ///     succeeded, so the project is untouched when it does.
    /// </summary>
    public ImportResult ImportSequenceAsTrack(Sequence sequence, string name,
        IReadOnlyDictionary<string, Sound>? soundMap, TrackKind kind = TrackKind.PianoRoll)
    {
        var result = kind == TrackKind.Faithful
            ? SequenceImporter.AddAsFaithfulTrack(Project, sequence, name, soundMap)
            : SequenceImporter.AddAsTrack(Project, sequence, name, soundMap);
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

    /// <summary>
    ///     Rebuilds a track as the other kind, through its own exported sequence - the one
    ///     representation both kinds already agree on, so the converted track plays the same.
    ///     It keeps the original's name, colour, transpose, place in the list and every clip
    ///     it had. Two things a round trip does not survive, both because a TDW sequence
    ///     doesn't hold them: the piano roll's grid fitting, and trailing silence - a
    ///     converted clip ends on its last sound rather than at a segment boundary.
    ///     Throws whatever <see cref="SequenceImporter" /> throws on a track it can't convert
    ///     (an empty one, a piano roll with no sounds) - nothing is changed when it does.
    /// </summary>
    public ProjectTrack ConvertTrack(ProjectTrack track)
    {
        var kind = track.Kind == TrackKind.Faithful ? TrackKind.PianoRoll : TrackKind.Faithful;
        var sequence = track.ToSequence();
        var (converted, instruments, _, _) = kind == TrackKind.Faithful
            ? SequenceImporter.AddAsFaithfulTrack(Project, sequence, track.Name, null)
            : SequenceImporter.AddAsTrack(Project, sequence, track.Name, null);

        ArgumentNullException.ThrowIfNull(converted);

        converted.Name = track.Name;
        converted.ColorIndex = track.ColorIndex;
        converted.Transpose = track.Transpose;

        var index = IndexOf(Project.Tracks, track);
        var oldPlacements = Project.Placements.Where(placement => placement.Track == track).ToArray();
        var instrumentIndices = instruments.Select(instrument => IndexOf(Project.Instruments, instrument)).ToArray();

        // The importer appended its track (with a clip on a fresh channel) at the end; a
        // conversion takes the old track's slot and its clips instead.
        Project.RemoveTrack(converted);
        Project.RemoveTrack(track);
        Project.AddTrack(converted, index);
        var newPlacements = oldPlacements
            .Select(placement => Project.Place(converted, placement.Channel, placement.StartQuarterNotes))
            .ToArray();

        Retarget(track, converted, oldPlacements);

        _undoHistory.Push(
            () =>
            {
                Project.RemoveTrack(converted);
                Project.AddTrack(track, index);
                foreach (var placement in oldPlacements) Project.AddPlacement(placement);
                foreach (var instrument in instruments) Project.RemoveInstrument(instrument);
                Retarget(converted, track, newPlacements);
            },
            () =>
            {
                Project.RemoveTrack(track);
                for (var i = 0; i < instruments.Count; i++) Project.AddInstrument(instruments[i], instrumentIndices[i]);
                Project.AddTrack(converted, index);
                foreach (var placement in newPlacements) Project.AddPlacement(placement);
                Retarget(track, converted, oldPlacements);
            });

        Touch();
        return converted;

        // Whatever pointed at the track that just went away has to point at the one that
        // replaced it, or the list highlight and the open panel address a dropped track.
        void Retarget(ProjectTrack from, ProjectTrack to, TrackPlacement[] dropped)
        {
            var open = OpenedTrack == from;
            if (open) CloseTrack();
            _tracks.Replace(from, to);
            RemoveFromPlacementSelection(dropped);
            if (open) OpenTrack(to);
        }
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
            : InstrumentsOf(track).FirstOrDefault();
        OnOpenedTrackChanged?.Invoke(track);
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
}

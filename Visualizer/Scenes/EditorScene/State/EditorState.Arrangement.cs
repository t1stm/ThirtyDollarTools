using ThirtyDollarConverter.Editor;

namespace EditorScene.State;

/// <summary>
///     The arrangement side: clips (<see cref="TrackPlacement" />) on channels, and moving or
///     nudging a whole clip selection at once.
/// </summary>
public partial class EditorState
{
    public TrackPlacement PlaceTrack(ProjectTrack track, int channel, double startQuarterNotes)
    {
        var placement = Project.Place(track, channel, startQuarterNotes);
        _undoHistory.Push(
            () => Project.RemovePlacement(placement),
            () => Project.AddPlacement(placement));
        Touch();
        return placement;
    }

    public bool RemovePlacement(TrackPlacement placement)
    {
        if (!Project.RemovePlacement(placement)) return false;
        _placements.Remove([placement]);
        _undoHistory.Push(
            () => Project.AddPlacement(placement),
            () => Project.RemovePlacement(placement));
        Touch();
        return true;
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
    ///     The arrangement's counterpart to <see cref="NudgeNotes" />: the selected clips
    ///     move by <paramref name="startDelta" /> quarter notes and whole channels, clamped
    ///     to the start of the timeline and to <paramref name="maxChannel" />.
    /// </summary>
    public void NudgePlacements(double startDelta, int channelDelta, int maxChannel)
    {
        if (_placements.Count == 0) return;

        BeginGesture();
        MoveSelectedPlacements(_placements.Items.Select(placement => (
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
}

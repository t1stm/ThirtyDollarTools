namespace ThirtyDollarConverter.Editor;

/// <summary>
///     One clip on the arrangement grid: a pattern (<see cref="ProjectTrack" />) placed
///     on a channel at a position on the project timeline. The same pattern can be
///     placed any number of times; every clip references the same track instance, so
///     editing the pattern updates all of its clips.
/// </summary>
public class TrackPlacement(ProjectTrack track, int channel, double startQuarterNotes)
{
    public ProjectTrack Track { get; } = track;

    /// <summary>The arrangement lane. Purely visual — channels carry no audio meaning.</summary>
    public int Channel { get; set; } = channel;

    /// <summary>
    ///     Position on the project timeline in quarter notes at the root BPM — the
    ///     library's tempo anchor unit, so the value is exact for grid-snapped drags.
    /// </summary>
    public double StartQuarterNotes { get; set; } = startQuarterNotes;
}

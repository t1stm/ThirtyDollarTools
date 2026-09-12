using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Editor;

/// <summary>
///     A WAVE file placed on the arrangement as a reference while writing - the recording a
///     project is being transcribed from, playing under it. It holds no sounds and no tempo:
///     <see cref="TimedNotes" /> and <see cref="TempoRegions" /> are empty, so the sequence
///     (and with it the export) never learns it exists. Its audio is decoded and played by the
///     editor's playback session, alongside the rendered mix rather than inside it.
///     The file plays at its own rate forever; the project's BPM only decides how many quarter
///     notes <see cref="DurationMinutes" /> covers, i.e. how wide the clip is on the grid.
/// </summary>
public sealed class WaveTrack(TimingInfo timing, int id) : ProjectTrack(timing, id)
{
    public override TrackKind Kind => TrackKind.Wave;

    /// <summary>
    ///     Where the file is. Decoding happens in the editor, never here - the model keeps the
    ///     path and the length so a project loads (and its clip keeps its width) whether or not
    ///     the file is still on this machine.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>The file's real length. Saved with the project, so a missing file still draws.</summary>
    public double DurationSeconds { get; set; }

    /// <summary>Playback gain, 0-100 like every other volume here. Reference-only, never exported.</summary>
    public double Volume { get; set; } = 100;

    public override double DurationMinutes()
    {
        return DurationSeconds / 60d;
    }

    /// <summary>The file is not a sequence, so there is nothing for a per-track render to play.</summary>
    public override Sequence ToSequence(SequenceStyle? style = null, bool autoResume = true)
    {
        return new Sequence();
    }

    /// <summary>No grid, so no bar dividers - same as a faithful track.</summary>
    internal override double[]? BarTimes(SequenceStyle? style)
    {
        return null;
    }

    internal override IEnumerable<(double Minutes, BaseEvent Event)> TimedNotes(double startMinutes = 0,
        float projectTranspose = 0, IReadOnlyList<CutPoint>? cuts = null)
    {
        return [];
    }

    /// <summary>
    ///     Empty on purpose: a reference track must not pull the merged timeline onto a grid
    ///     rate of its own. Callers that walk every track's regions have to survive the empty
    ///     list - see <see cref="ThirtyDollarProject.MergedRegions" />.
    /// </summary>
    internal override List<TempoRegion> TempoRegions(double startMinutes = 0)
    {
        return [];
    }

    internal override ProjectTrack Duplicate(int id, string name)
    {
        return new WaveTrack(Timing, id)
        {
            Name = name,
            ColorIndex = ColorIndex,
            Path = Path,
            DurationSeconds = DurationSeconds,
            Volume = Volume
        };
    }
}

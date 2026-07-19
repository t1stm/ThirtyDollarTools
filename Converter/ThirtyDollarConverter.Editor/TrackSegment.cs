namespace ThirtyDollarConverter.Editor;

/// <summary>
///     A consecutive region of a track with its own time signature and grid resolution.
///     Inherits the track's BPM, anchored to the quarter note — so a 7/8 bar lasts
///     3.5 quarter notes regardless of the signature.
/// </summary>
public class TrackSegment
{
    public int Numerator { get; set; } = 4;
    public int Denominator { get; set; } = 4;

    /// <summary>
    ///     Optional tempo override in quarter-note BPM. Null inherits the track's BPM.
    /// </summary>
    public float? BPM { get; set; }

    /// <summary>
    ///     How many bars this segment spans before the next segment starts.
    /// </summary>
    public int Bars { get; set; } = 1;

    /// <summary>
    ///     Grid resolution: steps per beat, where one beat is a 1/<see cref="Denominator" /> note.
    /// </summary>
    public int StepsPerBeat { get; set; } = 4;

    /// <summary>
    ///     The editable notes of this segment. Steps are local to the segment's start.
    /// </summary>
    public List<Note> Notes { get; } = [];

    /// <summary>
    ///     Total grid steps this segment spans — the width of its editing grid.
    /// </summary>
    public int StepCount => Bars * Numerator * StepsPerBeat;

    /// <summary>
    ///     Length of one grid step in minutes, at the given quarter-note BPM.
    /// </summary>
    public double StepMinutes(double trackBpm)
    {
        return 4d / (Denominator * (BPM ?? trackBpm) * StepsPerBeat);
    }

    /// <summary>
    ///     Length of the whole segment in minutes, at the given quarter-note BPM.
    /// </summary>
    internal double DurationMinutes(double trackBpm)
    {
        return Bars * Numerator * StepsPerBeat * StepMinutes(trackBpm);
    }
}
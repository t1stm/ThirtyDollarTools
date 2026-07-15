using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

/// <summary>
///     A single note on a track's grid. Mutable so the GUI can hold it as a stable handle
///     while dragging it around the piano roll.
/// </summary>
public class Note
{
    /// <summary>
    ///     Position on the track's grid, in steps of 1 / <see cref="ProjectTrack.StepsPerBeat" /> beats.
    /// </summary>
    public required int Step { get; set; }

    /// <summary>
    ///     The TDW sound this note plays.
    /// </summary>
    public required string Sound { get; set; }

    /// <summary>
    ///     Pitch offset in semitones.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    ///     Volume in percent. Null follows the sequence volume.
    /// </summary>
    public double? Volume { get; set; }

    /// <summary>
    ///     Stereo pan, -100 (left) to 100 (right).
    /// </summary>
    public float Pan { get; set; }

    /// <summary>
    ///     Optional automation that generates follow-up events (echo, sustain, fades)
    ///     from this note. Managers are stateless — one instance can be shared by all
    ///     notes of a segment.
    /// </summary>
    public AudioKeyframeManager? Automation { get; set; }

    internal BaseEvent ToEvent()
    {
        if (Pan == 0)
            return new NormalEvent
            {
                SoundEvent = Sound,
                Value = Value,
                WorkingValue = Value,
                Volume = Volume,
                ValueScale = ValueScale.None
            };

        return new ExtendedEvent
        {
            SoundEvent = Sound,
            Value = Value,
            WorkingValue = Value,
            Volume = Volume,
            ValueScale = ValueScale.None,
            Pan = Pan
        };
    }
}

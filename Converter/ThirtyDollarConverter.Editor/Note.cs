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
    ///     The instrument this note plays.
    /// </summary>
    public required Instrument Instrument { get; set; }

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
    ///     Offset of the sound's start in seconds (the TDW "&gt;" extension).
    /// </summary>
    public double Offset { get; set; }

    /// <summary>
    ///     Optional automation that generates follow-up events (echo, sustain, fades)
    ///     from this note. Managers are stateless — one instance can be shared by all
    ///     notes of a segment.
    /// </summary>
    public AudioKeyframeManager? Automation { get; set; }

    /// <summary>One event per instrument sound, layered on this note's step. Empty instrument -> none.</summary>
    internal IEnumerable<BaseEvent> ToEvents()
    {
        foreach (var sound in Instrument.Sounds)
        {
            var adjustment = Instrument.Adjustments.GetValueOrDefault(sound);
            var value = adjustment?.CombineValue(Value) ?? Value;
            var volume = adjustment?.CombineVolume(Volume) ?? Volume;
            var pan = adjustment?.CombinePan(Pan) ?? Pan;

            if (pan == 0 && Offset == 0)
                yield return new NormalEvent
                {
                    SoundEvent = sound,
                    Value = value,
                    WorkingValue = value,
                    Volume = volume,
                    ValueScale = ValueScale.None
                };
            else
                yield return new ExtendedEvent
                {
                    SoundEvent = sound,
                    Value = value,
                    WorkingValue = value,
                    Volume = volume,
                    ValueScale = ValueScale.None,
                    Pan = pan,
                    OffsetInSeconds = Offset
                };
        }
    }
}
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

    /// <summary>
    ///     True when this note is a cut (retrigger) instead of a play: it silences every
    ///     one of <see cref="Instrument" />'s sounds instead of playing them, and carries
    ///     no meaningful Value/Volume/Pan/Offset/Automation of its own — those fields are
    ///     enforced at default at creation (not by a separate type), same reserved-instrument
    ///     mechanism the sound-picker can never reach on its own.
    /// </summary>
    public bool IsCut { get; set; }

    /// <summary>
    ///     Deep copy: Automation is cloned so editing one note's keyframes can't reach the
    ///     other. Instrument stays referenced (shared project resource, not owned by the note).
    /// </summary>
    public Note Duplicate()
    {
        return new Note
        {
            Step = Step,
            Instrument = Instrument,
            Value = Value,
            Volume = Volume,
            Pan = Pan,
            Offset = Offset,
            Automation = Automation?.Clone(),
            IsCut = IsCut
        };
    }

    /// <summary>
    ///     One event per instrument sound, layered on this note's step — unless this is a
    ///     cut, which instead yields one <see cref="IndividualCutEvent" /> silencing every
    ///     one of the instrument's sounds at once (the same mechanism
    ///     <see cref="AudioKeyframeManager" />'s "Cut" keyframe already uses). Empty
    ///     instrument -&gt; no events either way.
    /// </summary>
    internal IEnumerable<BaseEvent> ToEvents()
    {
        if (IsCut)
        {
            if (Instrument.Sounds.Count > 0)
                yield return new IndividualCutEvent(Instrument.Sounds.ToHashSet());
            yield break;
        }

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
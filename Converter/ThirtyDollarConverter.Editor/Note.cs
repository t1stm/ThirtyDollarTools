using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

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
    ///     from this note. Managers are stateless - one instance can be shared by all
    ///     notes of a segment.
    /// </summary>
    public AudioKeyframeManager? Automation { get; set; }

    /// <summary>
    ///     Auto offset: how far the sound has already played when this (generated) note
    ///     starts, in seconds of the sound at the note's own pitch. Each instrument sound
    ///     scales it by its own value, because a sound's pitch decides how much of it one
    ///     second of playback consumes. Set only by <see cref="AudioKeyframeManager" />.
    /// </summary>
    internal double AutoOffsetSeconds { get; init; }

    /// <summary>
    ///     True when this note is a cut (retrigger) instead of a play: it silences every
    ///     one of <see cref="Instrument" />'s sounds instead of playing them, and carries
    ///     no meaningful Value/Volume/Pan/Offset/Automation of its own - those fields are left
    ///     at their defaults when the note is created.
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
    ///     One event per instrument sound, layered on this note's step - unless this is a
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
                yield return new IndividualCutEvent(Instrument.SoundNames);
            yield break;
        }

        foreach (var instrument_sound in Instrument.Sounds)
        {
            var sound = instrument_sound.Sound;
            var value = instrument_sound.CombineValue(Value);
            var volume = instrument_sound.CombineVolume(Volume);
            var pan = instrument_sound.CombinePan(Pan);
            // The encoder skips (offset x sample rate / 2^(value/12)) samples of the sound's
            // own buffer, so the offset counts seconds of the unpitched sound: an auto offset
            // measured in playback time has to be scaled by this sound's own pitch.
            var offset = AutoOffsetSeconds == 0
                ? Offset
                : Offset + AutoOffsetSeconds * Math.Pow(2, instrument_sound.Value / 12);

            if (pan == 0 && offset == 0)
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
                    OffsetInSeconds = offset
                };
        }
    }
}
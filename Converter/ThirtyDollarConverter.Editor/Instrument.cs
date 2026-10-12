namespace ThirtyDollarConverter.Editor;

/// <summary>
///     A named set of sounds. A note plays an instrument's sounds all layered on one step,
///     instead of a single sound.
/// </summary>
public class Instrument
{
    public int Id { get; set; }
    public string Name { get; set; } = "Instrument";

    /// <summary>
    ///     Ordered; layered on one step (first plain, rest !combine-joined). The same sound
    ///     may appear several times with different tuning (see <see cref="InstrumentSound" />).
    ///     May be empty transiently in the editor; an empty instrument yields no events.
    /// </summary>
    public List<InstrumentSound> Sounds { get; } = [];

    /// <summary>Pitch offset in semitones, added to the playing note's for every one of this instrument's sounds.</summary>
    public double Value { get; set; }

    /// <summary>
    ///     Volume in percent, scaling the playing note's for every one of this instrument's sounds,
    ///     the way <see cref="InstrumentSound.Volume" /> does. Null follows the note.
    /// </summary>
    public double? Volume { get; set; }

    /// <summary>Stereo pan, added to the playing note's for every one of this instrument's sounds.</summary>
    public float Pan { get; set; }

    /// <summary>
    ///     Sound-start offset in seconds, added to the playing note's own
    ///     <see cref="Note.Offset" /> for every one of this instrument's sounds.
    /// </summary>
    public double Offset { get; set; }

    /// <summary>
    ///     What <paramref name="sound" /> plays as on a note carrying the given values: the note's,
    ///     then this instrument's, then the sound's own. Value and offset add; pan adds and clamps
    ///     to ±100; volume multiplies as percentages, a null at any level following the one before.
    ///     Playback, export and every preview go through here, so they can't disagree.
    /// </summary>
    /// <param name="autoOffsetSeconds">
    ///     See <see cref="Note.AutoOffsetSeconds" />: seconds of the sound at the note's pitch, so it
    ///     is scaled by the sound's effective pitch, this instrument's value plus its own.
    /// </param>
    public (double Value, double? Volume, float Pan, double Offset) Combine(InstrumentSound sound,
        double value = 0, double? volume = null, float pan = 0, double offset = 0, double autoOffsetSeconds = 0)
    {
        var own_pitch = Value + sound.Value;

        // The encoder skips (offset x sample rate / 2^(value/12)) samples of the sound's own
        // buffer, so the offset counts seconds of the unpitched sound: an auto offset measured
        // in playback time has to be scaled by this sound's own pitch.
        var auto_offset = autoOffsetSeconds == 0 ? 0 : autoOffsetSeconds * Math.Pow(2, own_pitch / 12);

        return (value + own_pitch,
            InstrumentSound.CombineVolume(sound.Volume, InstrumentSound.CombineVolume(Volume, volume)),
            Math.Clamp(pan + Pan + sound.Pan, -100f, 100f),
            offset + auto_offset + Offset + sound.Offset);
    }

    /// <summary>
    ///     Every distinct sound name this instrument plays, duplicates collapsed -
    ///     what a cut has to silence.
    /// </summary>
    public HashSet<string> SoundNames => [.. Sounds.Select(sound => sound.Sound)];

    /// <summary>
    ///     Appends one plain, untuned sound - the common case; tune the returned
    ///     instance (or add it twice) for anything else.
    /// </summary>
    public InstrumentSound AddSound(string sound)
    {
        var instrument_sound = new InstrumentSound { Sound = sound };
        Sounds.Add(instrument_sound);
        return instrument_sound;
    }

    /// <summary>A standalone single-sound instrument, named after the sound.</summary>
    public static Instrument Single(string sound)
    {
        return new Instrument { Name = sound, Sounds = { new InstrumentSound { Sound = sound } } };
    }
}
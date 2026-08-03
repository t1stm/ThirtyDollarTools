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

    /// <summary>
    ///     Every distinct sound name this instrument plays, duplicates collapsed -
    ///     what a cut has to silence.
    /// </summary>
    public HashSet<string> SoundNames => Sounds.Select(sound => sound.Sound).ToHashSet();

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
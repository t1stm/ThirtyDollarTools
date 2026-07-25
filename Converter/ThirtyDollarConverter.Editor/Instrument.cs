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
    ///     Ordered; layered on one step (first plain, rest !combine-joined). May be empty
    ///     transiently in the editor; an empty instrument yields no events.
    /// </summary>
    public List<string> Sounds { get; } = [];

    /// <summary>
    ///     Per-sound value/volume/pan tuning, keyed by sound name. A sound absent here plays
    ///     with no adjustment (i.e. exactly what the note specifies).
    /// </summary>
    public Dictionary<string, SoundAdjustment> Adjustments { get; } = new();

    /// <summary>A standalone single-sound instrument, named after the sound.</summary>
    public static Instrument Single(string sound)
    {
        return new Instrument { Name = sound, Sounds = { sound } };
    }
}

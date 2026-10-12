namespace ThirtyDollarConverter.Editor;

/// <summary>
///     One sound inside an instrument, with its own value/volume/pan tuning applied on top
///     of whatever the playing note carries - a sound with its own value -3 plays at 0 on a
///     note at +3. An instrument may hold the same sound several times with different
///     tuning (e.g. 0 and -12 for dual-octave playback), so this is an instance, not a
///     lookup keyed by sound name.
/// </summary>
public class InstrumentSound
{
    public required string Sound { get; set; }

    public double Value { get; set; }

    /// <summary>
    ///     Volume in percent, scaling the note's own volume for this sound - a 50% sound on
    ///     a 50% note plays at 25%, the same way the sequence-wide volume scales an event's.
    ///     Null means the sound follows whatever volume the note carries.
    /// </summary>
    public double? Volume { get; set; }

    public float Pan { get; set; }

    /// <summary>Sound-start offset in seconds, added on top of the note's and the instrument's.</summary>
    public double Offset { get; set; }

    /// <summary>
    ///     Percent times percent: 50 on a note at 50 gives 25. A base that carries no volume of
    ///     its own leaves this one as-is - the sequence volume it follows is applied later,
    ///     multiplicatively all the same (see PlacementCalculator). A null
    ///     <paramref name="volume" /> follows the base.
    /// </summary>
    public static double? CombineVolume(double? volume, double? baseVolume)
    {
        if (volume is null) return baseVolume;
        return baseVolume is null ? volume : volume * baseVolume / 100;
    }

    public InstrumentSound Clone()
    {
        return new InstrumentSound { Sound = Sound, Value = Value, Volume = Volume, Pan = Pan, Offset = Offset };
    }
}
namespace ThirtyDollarConverter.Editor;

/// <summary>
///     Per-sound tuning within an instrument, applied on top of whatever value/volume/pan
///     the playing note carries — a sound with its own value -3 plays at 0 on a note at +3.
/// </summary>
public class SoundAdjustment
{
    public double Value { get; set; }

    /// <summary>Absolute volume in percent, overriding the note's own volume for this sound.
    /// Null means the sound follows whatever volume the note carries.</summary>
    public double? Volume { get; set; }

    public float Pan { get; set; }

    public bool IsNoOp => Value == 0 && Volume is null && Pan == 0;

    public double CombineValue(double baseValue)
    {
        return baseValue + Value;
    }

    public double? CombineVolume(double? baseVolume)
    {
        return Volume ?? baseVolume;
    }

    public float CombinePan(float basePan)
    {
        return Math.Clamp(basePan + Pan, -100f, 100f);
    }
}

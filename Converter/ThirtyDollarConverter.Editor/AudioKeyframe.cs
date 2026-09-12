namespace ThirtyDollarConverter.Editor;

public enum ModifierKind
{
    Add,
    Multiply
}

/// <summary>
///     A relative change to one field of a generated event. The default is Add 0 - a no-op.
/// </summary>
public readonly record struct Modifier(double Amount, ModifierKind Kind = ModifierKind.Add)
{
    public double Apply(double value)
    {
        return Kind == ModifierKind.Multiply ? value * Amount : value + Amount;
    }
}

/// <summary>
///     One automation point. Generates a copy of the running note state with each field
///     modified relative to the previous keyframe's result. Keyframes are created and
///     removed by <see cref="AudioKeyframeManager" /> as the note's length changes - they
///     are never added by hand.
/// </summary>
public class AudioKeyframe
{
    /// <summary>
    ///     Own position, in <see cref="AudioKeyframeManager.Timing" /> units from the note's
    ///     start, set by dragging this keyframe's marker sideways on the grid. Null (the
    ///     default) follows the derived grid position instead.
    /// </summary>
    public float? Position { get; set; }

    /// <summary>Pitch change in semitones.</summary>
    public Modifier Value { get; set; }

    /// <summary>Volume change in percent (the base note's null volume counts as 100).</summary>
    public Modifier Volume { get; set; }

    /// <summary>Pan change, result clamped to -100..100.</summary>
    public Modifier Pan { get; set; }

    /// <summary>
    ///     Change to the sound-start offset in seconds (the TDW "&gt;" extension). An
    ///     additive offset per keyframe walks the sound forward on every retrigger.
    /// </summary>
    public Modifier Offset { get; set; }

    /// <summary>
    ///     Ignore <see cref="Offset" /> and continue the sound from where the previous
    ///     instance reached, so a retrigger splices onto it seamlessly instead of restarting.
    ///     Only useful with the automation's cut, which silences the instance it continues.
    /// </summary>
    public bool AutoOffset { get; set; }

    /// <summary>Deep copy; <paramref name="keepPosition" /> false drops the grid override.</summary>
    public AudioKeyframe Clone(bool keepPosition = true)
    {
        return new AudioKeyframe
        {
            Position = keepPosition ? Position : null,
            Value = Value,
            Volume = Volume,
            Pan = Pan,
            Offset = Offset,
            AutoOffset = AutoOffset
        };
    }

    public bool ValueEquals(AudioKeyframe other)
    {
        return Position == other.Position && Value == other.Value && Volume == other.Volume &&
               Pan == other.Pan && Offset == other.Offset && AutoOffset == other.AutoOffset;
    }
}

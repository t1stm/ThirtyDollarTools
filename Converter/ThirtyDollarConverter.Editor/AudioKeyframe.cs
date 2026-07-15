namespace ThirtyDollarConverter.Editor;

public enum ModifierKind
{
    Add,
    Multiply
}

/// <summary>
///     A relative change to one field of a generated event. The default is Add 0 — a no-op.
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
///     modified relative to the previous keyframe's result.
/// </summary>
public class AudioKeyframe
{
    /// <summary>
    ///     Distance from the previous keyframe (or the note itself). Counted in grid steps
    ///     under <see cref="KeyframeTiming.Step" />, in seconds under <see cref="KeyframeTiming.Time" />.
    /// </summary>
    public float Gap { get; set; }

    /// <summary>Pitch change in semitones.</summary>
    public Modifier Value { get; set; }

    /// <summary>Volume change in percent (the base note's null volume counts as 100).</summary>
    public Modifier Volume { get; set; }

    /// <summary>Pan change, result clamped to -100..100.</summary>
    public Modifier Pan { get; set; }
}

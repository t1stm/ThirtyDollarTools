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
///     modified relative to the previous keyframe's result.
/// </summary>
public class AudioKeyframe
{
    /// <summary>
    ///     Distance from the previous keyframe (or the note itself). Counted in grid steps
    ///     under <see cref="KeyframeTiming.Step" />, in seconds under <see cref="KeyframeTiming.Time" />.
    /// </summary>
    public float Gap { get; set; }

    /// <summary>
    ///     When true, this keyframe cuts the note's instrument sounds immediately before
    ///     placing its note (both at the same position) - restarting a sustained/looped
    ///     sound cleanly instead of overlapping the new instance onto the old one.
    /// </summary>
    public bool Cut { get; set; }

    /// <summary>
    ///     Only meaningful with <see cref="Cut" />: the keyframe emits its cut and stops
    ///     there, placing no note of its own - the sound is silenced once the gap ends
    ///     instead of retriggered. The running value/volume/pan/offset still advance, so
    ///     later keyframes keep compounding as usual.
    /// </summary>
    public bool CutOnly { get; set; }

    /// <summary>
    ///     Only meaningful with <see cref="Cut" /> on the last keyframe: one extra cut is
    ///     emitted one more <see cref="Gap" /> after the automation's final note, so the
    ///     last beat is cut once it ends instead of ringing on past the automation.
    /// </summary>
    public bool CutLast { get; set; }

    /// <summary>Pitch change in semitones.</summary>
    public Modifier Value { get; set; }

    /// <summary>Volume change in percent (the base note's null volume counts as 100).</summary>
    public Modifier Volume { get; set; }

    /// <summary>Pan change, result clamped to -100..100.</summary>
    public Modifier Pan { get; set; }

    /// <summary>
    ///     Change to the sound-start offset in seconds (the TDW "&gt;" extension). An
    ///     additive offset per keyframe walks the sound forward on every repeat.
    /// </summary>
    public Modifier Offset { get; set; }
}
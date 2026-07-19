namespace ThirtyDollarConverter.Editor;

public enum KeyframeTiming
{
    /// <summary>Keyframe gaps count grid steps of the note's segment. Fractions allowed.</summary>
    Step,

    /// <summary>Keyframe gaps count seconds.</summary>
    Time
}

/// <summary>
///     Holds a note's automation keyframes. Applied once, in order: each keyframe fires
///     one generated event, spaced by its gap after the previous one, modifying the
///     previous result. Stateless during expansion, so one manager instance can be shared
///     by every note of a segment.
/// </summary>
public class AudioKeyframeManager
{
    public KeyframeTiming Timing { get; set; } = KeyframeTiming.Step;

    /// <summary>
    ///     How many times the keyframe list runs, back to back. Modifiers are relative,
    ///     so every pass keeps compounding the previous result — a ×0.5 volume echo
    ///     repeated 3 times decays to 12.5%. Values below 1 behave as 1.
    /// </summary>
    public int Repeats { get; set; } = 1;

    public List<AudioKeyframe> Keyframes { get; } = [];

    /// <summary>
    ///     Generates the automation events for one note placed at <paramref name="noteMinutes" />.
    ///     Public so views can plot the generated path (pass 0 for note-relative minutes).
    /// </summary>
    public IEnumerable<(double Minutes, Note Note)> Expand(Note note, double noteMinutes, double stepMinutes)
    {
        var minutes = noteMinutes;
        var value = note.Value;
        var volume = note.Volume ?? 100;
        var pan = note.Pan;
        var offset = note.Offset;

        for (var pass = 0; pass < Repeats; pass++)
        foreach (var keyframe in Keyframes)
        {
            minutes += Timing == KeyframeTiming.Step
                ? keyframe.Gap * stepMinutes
                : keyframe.Gap / 60d;

            value = keyframe.Value.Apply(value);
            volume = Math.Max(keyframe.Volume.Apply(volume), 0);
            pan = Math.Clamp((float)keyframe.Pan.Apply(pan), -100f, 100f);
            offset = keyframe.Offset.Apply(offset);

            yield return (minutes, new Note
            {
                Step = note.Step,
                Sound = note.Sound,
                Value = value,
                Volume = volume,
                Pan = pan,
                Offset = offset
            });
        }
    }
}
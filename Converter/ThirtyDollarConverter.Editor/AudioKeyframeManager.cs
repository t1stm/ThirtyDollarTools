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
    public List<AudioKeyframe> Keyframes { get; } = [];

    /// <summary>
    ///     Generates the automation events for one note placed at <paramref name="noteMinutes" />.
    /// </summary>
    internal IEnumerable<(double Minutes, Note Note)> Expand(Note note, double noteMinutes, double stepMinutes)
    {
        var minutes = noteMinutes;
        var value = note.Value;
        var volume = note.Volume ?? 100;
        var pan = note.Pan;

        foreach (var keyframe in Keyframes)
        {
            minutes += Timing == KeyframeTiming.Step
                ? keyframe.Gap * stepMinutes
                : keyframe.Gap / 60d;

            value = keyframe.Value.Apply(value);
            volume = Math.Max(keyframe.Volume.Apply(volume), 0);
            pan = Math.Clamp((float)keyframe.Pan.Apply(pan), -100f, 100f);

            yield return (minutes, new Note
            {
                Step = note.Step,
                Sound = note.Sound,
                Value = value,
                Volume = volume,
                Pan = pan
            });
        }
    }
}

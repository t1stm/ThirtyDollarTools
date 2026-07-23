using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

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
    ///     Deep copy: keyframes are mutable and edited in place by the inspector, so sharing
    ///     the list (or its entries) between notes would let editing one note's automation
    ///     change another's.
    /// </summary>
    public AudioKeyframeManager Clone()
    {
        var clone = new AudioKeyframeManager { Timing = Timing, Repeats = Repeats };
        foreach (var keyframe in Keyframes)
            clone.Keyframes.Add(new AudioKeyframe
            {
                Gap = keyframe.Gap,
                Cut = keyframe.Cut,
                Value = keyframe.Value,
                Volume = keyframe.Volume,
                Pan = keyframe.Pan,
                Offset = keyframe.Offset
            });
        return clone;
    }

    /// <summary>
    ///     Structural equality (Timing, Repeats, and every keyframe's fields) — used by the
    ///     multi-select inspector to decide whether several notes' automations are uniform
    ///     enough to edit as one. Reference equality doesn't apply: every note holds its own
    ///     cloned manager instance.
    /// </summary>
    public bool ValueEquals(AudioKeyframeManager other)
    {
        if (Timing != other.Timing || Repeats != other.Repeats) return false;
        if (Keyframes.Count != other.Keyframes.Count) return false;

        for (var i = 0; i < Keyframes.Count; i++)
        {
            var (a, b) = (Keyframes[i], other.Keyframes[i]);
            if (a.Gap != b.Gap || a.Cut != b.Cut) return false;
            if (a.Value != b.Value || a.Volume != b.Volume || a.Pan != b.Pan || a.Offset != b.Offset) return false;
        }

        return true;
    }

    /// <summary>
    ///     Generates the automation events for one note placed at <paramref name="noteMinutes" />,
    ///     flattened to the sound events each generated step actually plays (one per instrument
    ///     sound). A cut keyframe emits an individual cut immediately before its note, at the
    ///     same position, so the retrigger never overlaps the sound it's replacing. This is
    ///     what feeds the export/playback pipeline.
    /// </summary>
    public IEnumerable<(double Minutes, BaseEvent Event)> Expand(Note note, double noteMinutes, double stepMinutes)
    {
        foreach (var (minutes, generated, cut) in ExpandCore(note, noteMinutes, stepMinutes))
        {
            if (cut)
                yield return (minutes, new IndividualCutEvent(note.Instrument.Sounds.ToHashSet()));

            foreach (var ev in generated.ToEvents())
                yield return (minutes, ev);
        }
    }

    /// <summary>
    ///     Same generation, kept at the note level (not flattened to sound events) so views
    ///     can plot the generated value/time path. Public so views can plot the generated path
    ///     (pass 0 for note-relative minutes).
    /// </summary>
    public IEnumerable<(double Minutes, Note Note)> ExpandNotes(Note note, double noteMinutes, double stepMinutes)
    {
        foreach (var (minutes, generated, _) in ExpandCore(note, noteMinutes, stepMinutes))
            yield return (minutes, generated);
    }

    private IEnumerable<(double Minutes, Note Note, bool Cut)> ExpandCore(Note note, double noteMinutes,
        double stepMinutes)
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
                Instrument = note.Instrument,
                Value = value,
                Volume = volume,
                Pan = pan,
                Offset = offset
            }, keyframe.Cut);
        }
    }
}
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

public enum KeyframeTiming
{
    /// <summary>Gap, End and the automation offset count grid steps of the note's segment. Fractions allowed.</summary>
    Step,

    /// <summary>Gap, End and the automation offset count seconds.</summary>
    Time
}

/// <summary>
///     Holds a note's automation: the note's length (<see cref="End" />) and the retrigger
///     grid inside it (<see cref="Gap" />, <see cref="AutomationOffset" />). Keyframes are
///     generated from those three, one per grid position, and each fires one generated
///     event modifying the previous result. Stateless during expansion, so one manager
///     instance can be shared by every note of a segment.
/// </summary>
public class AudioKeyframeManager
{
    private readonly List<AudioKeyframe> _keyframes = [];
    private float _automationOffset;
    private float _end;
    private float _gap;

    public KeyframeTiming Timing { get; set; } = KeyframeTiming.Step;

    /// <summary>
    ///     Spacing between keyframes. Resizes the keyframe list. Zero - the default - means
    ///     no keyframes at all: a note given a length holds it without retriggering until a
    ///     gap is typed in.
    /// </summary>
    public float Gap
    {
        get => _gap;
        set
        {
            _gap = value;
            Sync();
        }
    }

    /// <summary>
    ///     Where the note ends - its length. The keyframe list is sized from this and
    ///     <see cref="Gap" />; 0 leaves a plain one-step note that generates nothing.
    /// </summary>
    public float End
    {
        get => _end;
        set
        {
            _end = value;
            Sync();
        }
    }

    /// <summary>
    ///     Shifts the whole keyframe grid, so chord voices that start at different steps
    ///     retrigger on the same instants. Needed because cuts are per sound name: a voice
    ///     whose keyframes land between another voice's keyframes is silenced by them.
    /// </summary>
    public float AutomationOffset
    {
        get => _automationOffset;
        set
        {
            _automationOffset = value;
            Sync();
        }
    }

    /// <summary>Cut the previous instance immediately before each keyframe.</summary>
    public bool Cut { get; set; } = true;

    /// <summary>One more cut at <see cref="End" />, so the note's length is audible.</summary>
    public bool CutAtEnd { get; set; } = true;

    /// <summary>What an auto-created keyframe starts as - the inspector's "All keyframes" card.</summary>
    public AudioKeyframe Template { get; set; } = new();

    /// <summary>
    ///     One entry per generated position, in time order. Auto-sized by <see cref="Sync" />;
    ///     entries are edited in place, never added or removed by callers.
    /// </summary>
    public IReadOnlyList<AudioKeyframe> Keyframes => _keyframes;

    /// <summary>
    ///     The note's length in grid steps - what the editor draws. Under
    ///     <see cref="KeyframeTiming.Time" /> the length is in seconds, so it depends on how
    ///     long a step of the note's own segment lasts.
    /// </summary>
    public float EndSteps(double stepMinutes)
    {
        if (Timing == KeyframeTiming.Step) return _end;
        return stepMinutes > 0 ? (float)(_end / 60d / stepMinutes) : 0;
    }

    /// <summary>
    ///     The keyframe's own <see cref="AudioKeyframe.Position" />, or the derived grid
    ///     position it sits on: the grid runs at <see cref="AutomationOffset" /> +
    ///     k × <see cref="Gap" /> for whole k, taking only the positions strictly between
    ///     the note's start and <see cref="End" />.
    /// </summary>
    public float PositionOf(int index)
    {
        return _keyframes[index].Position ?? DerivedPosition(index);
    }

    private float DerivedPosition(int index)
    {
        return _automationOffset + (FirstSlot + index) * _gap;
    }

    // Smallest whole k with AutomationOffset + k * Gap > 0 - the first grid position after
    // the note itself.
    private int FirstSlot => (int)Math.Floor(-_automationOffset / (double)_gap) + 1;

    private int DerivedCount()
    {
        if (_gap <= 0 || _end <= 0) return 0;
        // The epsilon keeps a keyframe from appearing exactly on End when a float division
        // lands a hair above the whole number of gaps (0.014 / 0.007 = 2.0000000176).
        var lastSlot = (int)Math.Ceiling((_end - _automationOffset) / (double)_gap - 1e-6) - 1;
        return Math.Max(0, lastSlot - FirstSlot + 1);
    }

    /// <summary>
    ///     Resizes the keyframe list to the grid. New entries copy <see cref="Template" />
    ///     (without its position); surplus entries are dropped from the tail, so shortening
    ///     and re-extending loses a hand-edited keyframe.
    /// </summary>
    private void Sync()
    {
        var count = DerivedCount();
        if (_keyframes.Count > count) _keyframes.RemoveRange(count, _keyframes.Count - count);
        while (_keyframes.Count < count) _keyframes.Add(Template.Clone(false));
    }

    /// <summary>
    ///     The generated value the automation has reached just before the keyframe at
    ///     <paramref name="index" /> - the note's own value for the first one. Modifiers are
    ///     relative, so this is what an editor needs to turn "put this keyframe at value v"
    ///     into the modifier that gets there.
    /// </summary>
    public double ValueBefore(Note note, int index)
    {
        var value = note.Value;
        for (var i = 0; i < index && i < _keyframes.Count; i++) value = _keyframes[i].Value.Apply(value);
        return value;
    }

    /// <summary>
    ///     Overwrites this automation with another's state, in place. Notes hold their
    ///     manager by reference, so undoing a keyframe edit has to restore the instance the
    ///     note is holding rather than swap in a new one.
    /// </summary>
    public void CopyFrom(AudioKeyframeManager other)
    {
        Timing = other.Timing;
        _gap = other._gap;
        _automationOffset = other._automationOffset;
        _end = other._end;
        Cut = other.Cut;
        CutAtEnd = other.CutAtEnd;
        Template = other.Template.Clone();
        _keyframes.Clear();
        foreach (var keyframe in other._keyframes) _keyframes.Add(keyframe.Clone());
    }

    /// <summary>
    ///     Deep copy: keyframes are mutable and edited in place by the inspector, so sharing
    ///     the list (or its entries) between notes would let editing one note's automation
    ///     change another's.
    /// </summary>
    public AudioKeyframeManager Clone()
    {
        var clone = new AudioKeyframeManager
        {
            Timing = Timing,
            _gap = _gap,
            _automationOffset = _automationOffset,
            _end = _end,
            Cut = Cut,
            CutAtEnd = CutAtEnd,
            Template = Template.Clone()
        };
        foreach (var keyframe in _keyframes) clone._keyframes.Add(keyframe.Clone());
        return clone;
    }

    /// <summary>
    ///     Structural equality - used by the multi-select inspector to decide whether several
    ///     notes' automations are uniform enough to edit as one. Reference equality doesn't
    ///     apply: every note holds its own cloned manager instance.
    /// </summary>
    public bool ValueEquals(AudioKeyframeManager other)
    {
        if (Timing != other.Timing || _gap != other._gap || _end != other._end ||
            _automationOffset != other._automationOffset || Cut != other.Cut || CutAtEnd != other.CutAtEnd)
            return false;
        if (!Template.ValueEquals(other.Template)) return false;
        if (_keyframes.Count != other._keyframes.Count) return false;

        for (var i = 0; i < _keyframes.Count; i++)
            if (!_keyframes[i].ValueEquals(other._keyframes[i]))
                return false;

        return true;
    }

    /// <summary>
    ///     Generates the automation events for one note placed at <paramref name="noteMinutes" />,
    ///     flattened to the sound events each generated step actually plays (one per instrument
    ///     sound). With <see cref="Cut" /> each keyframe cuts the note's instrument sounds
    ///     immediately before placing its note (both at the same position), so the retrigger
    ///     never overlaps the sound it replaces; <see cref="CutAtEnd" /> adds one last cut at
    ///     <see cref="End" />. This is what feeds the export/playback pipeline.
    /// </summary>
    public IEnumerable<(double Minutes, BaseEvent Event)> Expand(Note note, double noteMinutes, double stepMinutes)
    {
        foreach (var (minutes, generated) in ExpandNotes(note, noteMinutes, stepMinutes))
        {
            if (Cut) yield return (minutes, new GeneratedCutEvent(note.Instrument.SoundNames));

            foreach (var ev in generated.ToEvents())
                yield return (minutes, ev);
        }

        // The note stops where it ends instead of ringing on past it.
        if (CutAtEnd && _end > 0)
            yield return (noteMinutes + PositionMinutes(_end, stepMinutes),
                new GeneratedCutEvent(note.Instrument.SoundNames));
    }

    /// <summary>
    ///     Same generation, kept at the note level (not flattened to sound events) so views
    ///     can plot the generated value/time path. Pass 0 for note-relative minutes.
    /// </summary>
    public IEnumerable<(double Minutes, Note Note)> ExpandNotes(Note note, double noteMinutes, double stepMinutes)
    {
        var value = note.Value;
        var volume = note.Volume ?? 100;
        var pan = note.Pan;
        var offset = note.Offset;
        // How far the sound has played since the instance this one continues - reset by
        // every keyframe that starts the sound over instead of splicing onto it.
        var autoSeconds = 0d;
        var lastMinutes = noteMinutes;

        for (var i = 0; i < _keyframes.Count; i++)
        {
            var keyframe = _keyframes[i];
            var minutes = noteMinutes + PositionMinutes(PositionOf(i), stepMinutes);
            // The playing instance consumed the interval at the pitch it was playing at.
            autoSeconds += (minutes - lastMinutes) * 60d * Math.Pow(2, value / 12);
            lastMinutes = minutes;

            value = keyframe.Value.Apply(value);
            volume = Math.Max(keyframe.Volume.Apply(volume), 0);
            pan = Math.Clamp((float)keyframe.Pan.Apply(pan), -100f, 100f);
            if (!keyframe.AutoOffset)
            {
                offset = keyframe.Offset.Apply(offset);
                autoSeconds = 0;
            }

            yield return (minutes, new Note
            {
                Step = note.Step,
                Instrument = note.Instrument,
                Value = value,
                Volume = volume,
                Pan = pan,
                Offset = offset,
                AutoOffsetSeconds = autoSeconds
            });
        }
    }

    private double PositionMinutes(float position, double stepMinutes)
    {
        return Timing == KeyframeTiming.Step ? position * stepMinutes : position / 60d;
    }
}

/// <summary>
///     A cut the editor generated itself: an automation retrigger guard, silencing the
///     previous instance of one note's instrument. It is never meant to touch the sounds
///     that start alongside it, so <see cref="SequenceBuilder" /> hoists it to the front of
///     its step and merges it with the step's other generated cuts. A cut the user wrote -
///     a faithful "!cut" item, a cut note - stays exactly where it was placed.
/// </summary>
internal sealed class GeneratedCutEvent(HashSet<string> cutSounds) : IndividualCutEvent(cutSounds)
{
    public override IndividualCutEvent Copy()
    {
        return new GeneratedCutEvent(CutSounds);
    }
}

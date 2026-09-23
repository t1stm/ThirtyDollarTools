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
///     A cut somewhere on the timeline: when it fires, which sounds it silences, and whether
///     the editor generated it (an automation retrigger guard) or the user wrote it (a cut
///     note, a faithful "!cut"). Passed to <see cref="AudioKeyframeManager.Expand" /> so a
///     long note can repair the cuts that were never aimed at it - see
///     <see cref="AudioKeyframeManager.ExpandNotes" />. Lists must be in time order.
/// </summary>
public readonly record struct CutPoint(double Minutes, IReadOnlySet<string> Sounds, bool Generated);

/// <summary>
///     One instance an automation starts: a keyframe's retrigger, or a resume repairing a
///     cut that belonged to another note. <paramref name="KeyframeIndex" /> is the keyframe's
///     index, or -1 for a resume - which is automatic and has nothing to edit, so the editor
///     draws it but hangs no handle on it. <paramref name="Sounds" /> is null for a retrigger
///     (the instrument plays whole) and the cut's sound set for a resume, so an instrument
///     sound the cut missed is not re-placed on top of itself.
/// </summary>
public readonly record struct GeneratedNote(double Minutes, Note Note, int KeyframeIndex, IReadOnlySet<string>? Sounds);

/// <summary>
///     Holds a note's automation: the note's length (<see cref="End" />) and the retrigger
///     grid inside it (<see cref="Gap" />, <see cref="AutomationOffset" />). Keyframes are
///     generated from those three, one per grid position, and each fires one generated
///     event modifying the previous result. Stateless during expansion, so one manager
///     instance can be shared by every note of a segment.
/// </summary>
public class AudioKeyframeManager
{
    /// <summary>
    ///     How close two instants have to be to count as the same one. A step is 2e-3 minutes
    ///     at a sixteenth grid, and SequenceBuilder rounds steps to 6 decimals, so anything
    ///     inside this lands on the same step of the exported sequence anyway.
    /// </summary>
    private const double CoincidentMinutes = 1e-9;

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
    ///     <paramref name="cuts" /> is the timeline's cuts, in time order: a cut in there that
    ///     belongs to another note silences this one too (TDW cuts by sound name), so the
    ///     expansion puts it back - see <see cref="ExpandNotes" />. Null skips that entirely and
    ///     generates exactly what the note's own automation says.
    /// </summary>
    public IEnumerable<(double Minutes, BaseEvent Event)> Expand(Note note, double noteMinutes, double stepMinutes,
        IReadOnlyList<CutPoint>? cuts = null)
    {
        foreach (var (minutes, generated, index, sounds) in ExpandNotes(note, noteMinutes, stepMinutes, cuts))
        {
            // A resume carries no cut of its own: the foreign cut it repairs is already at
            // that instant, and HoistCuts has already put it in front of everything there.
            if (Cut && index >= 0) yield return (minutes, new GeneratedCutEvent(note.Instrument.SoundNames));

            foreach (var ev in generated.ToEvents())
                if (sounds is null || sounds.Contains(ev.SoundEvent!))
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
    ///     Interleaved with the keyframes, in time order, are the <b>resumes</b>: a cut in
    ///     <paramref name="cuts" /> that belongs to another note silences this one as well,
    ///     because TDW cuts by sound name, so the cut sounds are re-placed at that instant.
    ///     Only inside the note's length, only across cuts the editor generated, and only
    ///     continuing the sample where the ringing instance's auto offset asks for it -
    ///     otherwise the sound restarts, which is what this note's own retriggers sound like.
    /// </summary>
    public IEnumerable<GeneratedNote> ExpandNotes(Note note, double noteMinutes, double stepMinutes,
        IReadOnlyList<CutPoint>? cuts = null)
    {
        var value = note.Value;
        var volume = note.Volume ?? 100;
        var pan = note.Pan;
        var offset = note.Offset;
        // How far the sound has played since the instance this one continues - reset by
        // every keyframe that starts the sound over instead of splicing onto it.
        var autoSeconds = 0d;
        var lastMinutes = noteMinutes;
        // The instance currently ringing, and whether it wants to be spliced back onto: the
        // note itself until the first keyframe replaces it, so the automation's Template
        // speaks for the stretch before any keyframe exists.
        var ringing = note;
        var ringingMinutes = noteMinutes;
        var ringingSplices = Template.AutoOffset;
        // Nothing outside the note's declared length is resumed; End of 0 is a plain note,
        // which has no length to be inside of.
        var endMinutes = _end > 0 ? noteMinutes + PositionMinutes(_end, stepMinutes) : (double?)null;

        for (var i = 0; i < _keyframes.Count; i++)
        {
            var keyframe = _keyframes[i];
            var minutes = noteMinutes + PositionMinutes(PositionOf(i), stepMinutes);

            foreach (var resume in Resumes(note, cuts, ringing, ringingMinutes, ringingSplices, minutes))
                yield return resume;

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

            var generated = new Note
            {
                Step = note.Step,
                Instrument = note.Instrument,
                Value = value,
                Volume = volume,
                Pan = pan,
                Offset = offset,
                AutoOffsetSeconds = autoSeconds
            };

            yield return new GeneratedNote(minutes, generated, i, null);

            ringing = generated;
            ringingMinutes = minutes;
            ringingSplices = keyframe.AutoOffset;
        }

        // The last stretch: from whatever is ringing to the note's end.
        if (endMinutes is not { } end) yield break;

        foreach (var resume in Resumes(note, cuts, ringing, ringingMinutes, ringingSplices, end))
            yield return resume;
    }

    /// <summary>
    ///     Repairs the foreign cuts landing strictly between the ringing instance's start and
    ///     <paramref name="until" />: each one re-places the sounds it silenced at the instant
    ///     it silenced them. <paramref name="splices" /> - the ringing instance's auto offset -
    ///     decides whether the sound continues from where it had reached or starts over; the
    ///     elapsed time is measured at the pitch it was playing at, the same way
    ///     <see cref="ExpandNotes" /> measures its own.
    ///     A cut the user wrote ends the stretch instead of being repaired: it was aimed at
    ///     this sound on purpose, and the note is left silent until it retriggers itself.
    /// </summary>
    private static IEnumerable<GeneratedNote> Resumes(Note note, IReadOnlyList<CutPoint>? cuts, Note ringing,
        double ringingMinutes, bool splices, double until)
    {
        if (cuts is null) yield break;
        var sounds = note.Instrument.SoundNames;
        if (sounds.Count == 0) yield break;

        // Open at both ends: a cut on an own start is the retrigger's own guard, and one on End
        // is the note ending. The tolerance matches the 6-decimal step SequenceBuilder quantizes
        // to, so a coincidence that survives serialization counts as one here.
        // The cuts are in time order (see CutPoint), so the window is found by bisection and
        // left at its end. A scan of every cut per window made a project with a few hundred
        // long notes and a few thousand cuts spend ~200 ms here on every edit.
        int first = 0, last = cuts.Count;
        while (first < last)
        {
            var middle = (first + last) / 2;
            if (cuts[middle].Minutes <= ringingMinutes + CoincidentMinutes) first = middle + 1;
            else last = middle;
        }

        for (var i = first; i < cuts.Count; i++)
        {
            var cut = cuts[i];
            if (cut.Minutes >= until - CoincidentMinutes) yield break;
            if (!cut.Sounds.Overlaps(sounds)) continue;
            if (!cut.Generated) yield break;

            var elapsed = (cut.Minutes - ringingMinutes) * 60d * Math.Pow(2, ringing.Value / 12);
            yield return new GeneratedNote(cut.Minutes, new Note
            {
                Step = ringing.Step,
                Instrument = ringing.Instrument,
                Value = ringing.Value,
                Volume = ringing.Volume,
                Pan = ringing.Pan,
                Offset = ringing.Offset,
                AutoOffsetSeconds = splices ? ringing.AutoOffsetSeconds + elapsed : 0
            }, -1, cut.Sounds);
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

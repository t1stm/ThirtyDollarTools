using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

/// <summary>
///     One slot in a faithful sequence: either a sound (an instrument, played) or an action.
///     Exactly one of the two is set.
///     The <see cref="Note" />'s <see cref="Editor.Note.Step" /> is meaningless here - a faithful
///     sequence has no grid, position is the item's index - and stays 0. Actions are raw
///     <see cref="BaseEvent" />s on purpose (README's "never hand raw events to the editor model"
///     is waived for them): "!speed@2@x", "!pulse" and "!bg" carry a
///     <see cref="ValueScale" /> and packed two-value payloads that <see cref="Editor.Note" />
///     does not model. Sound items stay <see cref="Editor.Note" />s.
/// </summary>
public sealed class FaithfulItem
{
    public Note? Note { get; init; }
    public BaseEvent? Action { get; init; }

    public static FaithfulItem Sound(Instrument instrument)
    {
        return new FaithfulItem { Note = new Note { Step = 0, Instrument = instrument } };
    }

    /// <summary>
    ///     An action item from its TDW text ("!speed@2@x", "!bg@#ff0000,0.5"). Null when the
    ///     text holds no event - the same parser the saved file is read back with.
    /// </summary>
    public static FaithfulItem? Parse(string tdw)
    {
        var events = Sequence.FromString(tdw).Events;
        return events.Length == 0 ? null : new FaithfulItem { Action = events[0] };
    }

    public FaithfulItem Duplicate()
    {
        return new FaithfulItem { Note = Note?.Duplicate(), Action = Action?.Copy() };
    }
}

/// <summary>
///     A track edited the way https://thirtydollar.website does it: a linear sequence of
///     sounds and actions, no bar/beat grid. Its timing comes from walking the sequence
///     (<see cref="SequenceWalker" />) rather than from <see cref="TrackSegment" />s, so
///     "!speed", "!stop" and the loop/jump actions mean here exactly what they mean on the
///     site. Everything downstream - arrangement, playback, export, mute/solo - only ever
///     asks a track for <see cref="TimedNotes" /> and <see cref="TempoRegions" />, so
///     supplying those two is the whole integration.
/// </summary>
public sealed class FaithfulTrack(TimingInfo timing, int id) : ProjectTrack(timing, id)
{
    public override TrackKind Kind => TrackKind.Faithful;

    public List<FaithfulItem> Items { get; } = [];

    /// <summary>
    ///     The items as a TDW event stream: a sound item emits its instrument's sounds joined
    ///     with "!combine", an action item emits its event verbatim.
    /// </summary>
    internal IEnumerable<BaseEvent> Expand()
    {
        return ExpandTagged().Select(pair => pair.Event);
    }

    /// <summary>
    ///     <see cref="Expand" />, with the item each event came from - one event for an action,
    ///     several ("!combine"-joined) for a layered instrument. What a view drawing the
    ///     expanded stream needs to map a click back onto the slot the user sees.
    /// </summary>
    public IEnumerable<(FaithfulItem Item, BaseEvent Event)> ExpandTagged()
    {
        foreach (var item in Items)
        {
            if (item.Action is { } action)
            {
                // A bare "!cut" silences whatever is playing on the site; here it silences
                // every sound this track can play. That also makes it an IndividualCutEvent -
                // the only cut form editor playback forwards across channels - so an isolated
                // channel's preview cuts the same things the merged export does.
                yield return (item, action is not IndividualCutEvent && action.SoundEvent == "!cut" &&
                                    TrackSounds() is { Count: > 0 } sounds
                    ? new IndividualCutEvent(sounds)
                    : action.Copy());
                continue;
            }

            if (item.Note is not { } note) continue;

            var first = true;
            foreach (var ev in note.ToEvents())
            {
                if (!first) yield return (item, Combine());
                yield return (item, ev);
                first = false;
            }
        }
    }

    /// <summary>Every sound any of this track's sound items can play - what a bare "!cut" silences.</summary>
    private HashSet<string> TrackSounds()
    {
        return [.. Items.Select(item => item.Note?.Instrument).OfType<Instrument>().SelectMany(i => i.SoundNames)];
    }

    /// <summary>
    ///     When each expanded slot is played, in minutes from the track's start, paired with
    ///     its index in <see cref="ExpandTagged" />'s stream. A loop reports the same index
    ///     once per pass - what a view needs to bounce the slot the playhead is on.
    /// </summary>
    public IEnumerable<(double Minutes, int Index)> PlayTimes()
    {
        var walk = Walk();
        return walk.Events.Select(walked => (walk.MinutesOf(walked), walked.Source)).ToArray();
    }

    private WalkedSequence Walk()
    {
        return SequenceWalker.Walk([.. Expand()]);
    }

    public override double DurationMinutes()
    {
        return Walk().DurationMinutes;
    }

    /// <summary>The sequence has no bars, so it contributes no bar dividers.</summary>
    internal override double[]? BarTimes(SequenceStyle? style)
    {
        return null;
    }

    internal override bool ReferencesInstrument(Instrument instrument)
    {
        return Items.Any(item => item.Note?.Instrument == instrument);
    }

    internal override IEnumerable<(double Minutes, BaseEvent Event)> TimedNotes(double startMinutes = 0,
        float projectTranspose = 0)
    {
        var transpose = Transpose ?? projectTranspose;
        var walk = Walk();

        foreach (var walked in walk.Events)
        {
            // Safe to mutate: the walker hands out a fresh copy per emitted event.
            if (transpose != 0 && walked.IsSound)
            {
                walked.Event.Value += transpose;
                walked.Event.WorkingValue = walked.Event.Value;
            }

            yield return (startMinutes + walk.MinutesOf(walked), walked.Event);
        }
    }

    internal override List<TempoRegion> TempoRegions(double startMinutes = 0)
    {
        return Walk().ToTempoRegions(startMinutes);
    }

    internal override ProjectTrack Duplicate(int id, string name)
    {
        var copy = new FaithfulTrack(Timing, id) { Name = name, Transpose = Transpose, ColorIndex = ColorIndex };
        foreach (var item in Items) copy.Items.Add(item.Duplicate());
        return copy;
    }

    private static NormalEvent Combine()
    {
        return new NormalEvent { SoundEvent = "!combine", ValueScale = ValueScale.None };
    }
}

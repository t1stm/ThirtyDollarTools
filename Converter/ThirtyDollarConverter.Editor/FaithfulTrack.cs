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
    ///     its index in <see cref="ExpandTagged" />'s stream and the event as the walk saw it
    ///     (a "!loopmany" carries the passes it has left). A loop reports the same index once
    ///     per pass - what a view needs to animate the slot the playhead is on.
    ///     Every slot is here, the ones the walk consumes included: the site animates those
    ///     too. Read-only - these are the cached walk's own events, not copies.
    /// </summary>
    public IEnumerable<(double Minutes, int Index, BaseEvent Event)> PlayTimes()
    {
        var walk = Walk(true);
        return walk.Events.Select(walked => (walk.MinutesOf(walked), walked.Source, walked.Event)).ToArray();
    }

    private WalkedSequence? _walk;
    private long _timingSignature;
    private long _contentSignature;

    /// <summary>
    ///     The walk, kept until what it read changes. Walking an imported cover is ~20 ms, and
    ///     the editor asks a timing question after every edit.
    ///     Two signatures, because most edits only change half of what a walk holds: a
    ///     scrolled value or volume is in every event the walk hands out, but says nothing
    ///     about *when* anything plays. A duration, a tempo region or a play schedule can
    ///     therefore reuse a walk that an export could not - which is
    ///     <paramref name="timingOnly" />.
    ///     Content-addressed rather than invalidated by hand: <see cref="Items" />, the notes
    ///     in it and the instruments they play are all public and mutable, so there is no one
    ///     place a change could announce itself from.
    /// </summary>
    private WalkedSequence Walk(bool timingOnly = false)
    {
        var timing = TimingSignature();
        if (_walk is { } cached && timing == _timingSignature &&
            (timingOnly || ContentSignature() == _contentSignature))
            return cached;

        _walk = SequenceWalker.Walk([.. Expand()]);
        _timingSignature = timing;
        _contentSignature = ContentSignature();
        return _walk;
    }

    /// <summary>
    ///     A hash of everything that decides *when* a slot plays: the actions, and whether a
    ///     sound item takes a step at all (a cut is an action, and an empty instrument emits
    ///     nothing). A note's own value, volume, pan and offset are deliberately absent - the
    ///     walk carries them but never reads them.
    /// </summary>
    private long TimingSignature()
    {
        var hash = Basis;
        foreach (var item in Items)
        {
            if (item.Action is { } action)
            {
                Mix(ref hash, action.SoundEvent?.GetHashCode() ?? 0);
                Mix(ref hash, action.Value.GetHashCode());
                Mix(ref hash, action.WorkingValue.GetHashCode());
                Mix(ref hash, (int)action.ValueScale);
                continue;
            }

            if (item.Note is not { } note) continue;
            Mix(ref hash, note.IsCut ? 1 : 2);
            Mix(ref hash, note.Instrument.Sounds.Count == 0 ? 0 : 3);
        }

        return hash;
    }

    /// <summary>
    ///     A hash of everything else the expansion reads - what the walked events will say.
    ///     Only an export or playback needs this to still hold.
    /// </summary>
    private long ContentSignature()
    {
        var hash = Basis;
        foreach (var item in Items)
        {
            if (item.Action is { } action)
            {
                Mix(ref hash, action.Volume?.GetHashCode() ?? 0);
                // A bare "!cut" expands over every sound the track plays, which the notes
                // below already cover; a targeted one carries its own list.
                if (action is IndividualCutEvent cut)
                    foreach (var sound in cut.CutSounds)
                        Mix(ref hash, sound.GetHashCode());

                continue;
            }

            if (item.Note is not { } note) continue;

            Mix(ref hash, note.Value.GetHashCode());
            Mix(ref hash, note.Volume?.GetHashCode() ?? 0);
            Mix(ref hash, note.Pan.GetHashCode());
            Mix(ref hash, note.Offset.GetHashCode());

            foreach (var sound in note.Instrument.Sounds)
            {
                Mix(ref hash, sound.Sound.GetHashCode());
                Mix(ref hash, sound.Value.GetHashCode());
                Mix(ref hash, sound.Volume?.GetHashCode() ?? 0);
                Mix(ref hash, sound.Pan.GetHashCode());
            }
        }

        return hash;
    }

    /// <summary>FNV-1a 64's offset basis. 64 bits, not the 32 <see cref="HashCode" /> mixes:
    /// a collision here would keep a stale timing until the next real edit.</summary>
    private const long Basis = unchecked((long)14695981039346656037);

    private static void Mix(ref long hash, int value)
    {
        hash = unchecked((hash ^ value) * 1099511628211);
    }

    public override double DurationMinutes()
    {
        return Walk(true).DurationMinutes;
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
            // The walk reports what it swallowed so a view can animate it; re-emitting a
            // "!speed" or a "!combine" here would apply it a second time.
            if (walked.VisualOnly) continue;

            // A copy per call, not per walk: the walk is cached now, and everything
            // downstream treats these as its own - a transpose is written straight onto them,
            // and PlacementCalculator spends a "!stop"'s WorkingValue as it reads it.
            var copy = walked.Event.Copy();
            if (transpose != 0 && walked.IsSound)
            {
                copy.Value += transpose;
                copy.WorkingValue = copy.Value;
            }

            yield return (startMinutes + walk.MinutesOf(walked), copy);
        }
    }

    internal override List<TempoRegion> TempoRegions(double startMinutes = 0)
    {
        return Walk(true).ToTempoRegions(startMinutes);
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

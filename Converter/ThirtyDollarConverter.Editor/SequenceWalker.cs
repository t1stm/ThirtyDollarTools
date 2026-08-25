using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

/// <summary>
///     One event that survived the walk, positioned where the walk put it:
///     <paramref name="Step" /> counts grid steps from the start of <paramref name="Region" />.
///     Sounds carry the "!volume"/"!transpose" state baked in; every action the walk doesn't
///     consume itself (cuts, "!pulse", "!bg", "!divider", …) comes through verbatim and never
///     advances the position - exactly PlacementCalculator's classification.
///     <paramref name="Source" /> is the index in the walked stream this came from; a loop
///     reports the same one once per pass, which is how a view highlights the slot being played.
/// </summary>
internal readonly record struct WalkedEvent(int Region, double Step, BaseEvent Event, bool IsSound, int Source);

/// <summary>
///     The result of a walk: the timeline split into constant-speed regions (length in grid
///     steps) plus the events that landed in them. <see cref="Truncated" /> marks a walk that
///     hit <see cref="SequenceWalker.MaxWalkedEvents" /> and stopped - a runaway loop/jump.
/// </summary>
internal sealed record WalkedSequence(
    List<(double Speed, double Length)> Regions,
    List<WalkedEvent> Events,
    bool Truncated)
{
    /// <summary>Absolute start of every region in minutes; the last entry is the total length.</summary>
    private readonly double[] _starts = Starts(Regions);

    public double DurationMinutes => _starts[^1];

    public double MinutesOf(WalkedEvent walked)
    {
        return _starts[walked.Region] + walked.Step / Regions[walked.Region].Speed;
    }

    /// <summary>
    ///     The walk's regions as the timeline <see cref="SequenceBuilder" /> consumes. The BPM
    ///     stays 0 (unknown): a walked "!speed" is a raw rate that was never a tempo times a
    ///     resolution, so the export states it outright instead of inventing a split.
    /// </summary>
    public List<TempoRegion> ToTempoRegions(double startMinutes)
    {
        var regions = new List<TempoRegion>(Regions.Count);
        for (var r = 0; r < Regions.Count; r++)
            regions.Add(new TempoRegion(startMinutes + _starts[r], _starts[r + 1] - _starts[r], Regions[r].Speed));
        return regions;
    }

    private static double[] Starts(List<(double Speed, double Length)> regions)
    {
        var starts = new double[regions.Count + 1];
        for (var r = 0; r < regions.Count; r++)
            starts[r + 1] = starts[r] + regions[r].Length / regions[r].Speed;
        return starts;
    }
}

/// <summary>
///     Interprets a TDW event stream the way the website and <c>PlacementCalculator</c> do -
///     "!speed" / "!volume" / "!transpose" / "!stop" / "!looptarget" / "!loop" / "!loopmany" /
///     "!jump" / "!target" / "!combine" - and reports where every remaining event landed.
///     Shared by <see cref="SequenceImporter" /> (which turns the result into segments and
///     notes) and <see cref="FaithfulTrack" /> (which turns it into absolute times), so the
///     two can't drift apart on what a sequence means.
///     Never mutates the events it is handed: it walks a copy, and every event it reports is
///     a fresh copy too - a loop that replays one source event reports it once per pass.
/// </summary>
internal static class SequenceWalker
{
    /// <summary>
    ///     Hard ceiling on walked events, guarding against a hostile or malformed
    ///     loop/jump combination unrolling forever - a dropped file is a trust boundary.
    /// </summary>
    public const int MaxWalkedEvents = 1_000_000;

    private static readonly string[] JumpUntriggers = ["!loop", "!loopmany", "!jump", "!target"];
    private static readonly string[] LoopUntriggers = ["!loopmany", "!loop"];
    private static readonly string[] LoopmanyUntriggers = ["!loopmany"];

    public static WalkedSequence Walk(IReadOnlyList<BaseEvent> source)
    {
        var events = source.Select(ev => ev.Copy()).ToArray();
        var regions = new List<(double Speed, double Length)>();
        var walkedEvents = new List<WalkedEvent>();

        var speed = 300d;
        var position = 0d;
        var globalVolume = 100d;
        var transpose = 0d;
        var loopTarget = 0;
        var regionIndex = 0;
        var regionUsed = false;
        var truncated = false;

        var index = 0;
        var walked = 0;
        while (index < events.Length)
        {
            if (++walked > MaxWalkedEvents)
            {
                truncated = true;
                break;
            }

            var ev = events[index];
            var isAction = (ev.SoundEvent?.StartsWith('!') ?? true) || ev is ICustomActionEvent;

            if (!isAction)
            {
                var nextName = index + 1 < events.Length ? events[index + 1].SoundEvent : null;
                var advance = nextName is not "!combine";

                if (ev.SoundEvent is not "_pause")
                {
                    // A copy per emission: a loop passes over the same source event repeatedly,
                    // and baking transpose/volume into the walked array would compound.
                    var emitted = ev.Copy();
                    emitted.Value += transpose;
                    emitted.WorkingValue = emitted.Value;
                    if (globalVolume != 100) emitted.Volume = (emitted.Volume ?? 100) * globalVolume / 100;
                    walkedEvents.Add(new WalkedEvent(regionIndex, position, emitted, true, index));
                    regionUsed = true;
                }

                if (advance) position += 1;
                index++;
                continue;
            }

            switch (ev.SoundEvent)
            {
                case "!speed":
                {
                    var newSpeed = Scale(speed, ev);
                    if (!SequenceBuilder.SameSpeed(speed, newSpeed))
                    {
                        if (position > 1e-9) regions.Add((speed, position));
                        speed = newSpeed;
                        position = 0;
                        regionIndex = regions.Count;
                        regionUsed = false;
                    }

                    break;
                }
                case "!volume":
                {
                    globalVolume = Math.Max(0, Scale(globalVolume, ev));
                    break;
                }
                case "!transpose":
                {
                    transpose = Scale(transpose, ev);
                    break;
                }
                case "!stop":
                    position += ev.Value;
                    break;
                case "!looptarget":
                    loopTarget = index;
                    break;
                case "!loopmany":
                    if (ev.WorkingValue > 0)
                    {
                        ev.WorkingValue--;
                        index = loopTarget;
                        Untrigger(events, index, LoopmanyUntriggers);
                        continue;
                    }

                    break;
                case "!loop":
                    if (!ev.Triggered)
                    {
                        ev.Triggered = true;
                        index = loopTarget;
                        Untrigger(events, index, LoopUntriggers);
                        continue;
                    }

                    break;
                case "!jump":
                    if (!ev.Triggered)
                    {
                        ev.Triggered = true;
                        var target = Array.Find(events, e =>
                            e.SoundEvent == "!target" && Math.Abs(e.Value - ev.Value) < 0.001 && !e.Triggered);
                        if (target != null)
                        {
                            index = Array.IndexOf(events, target);
                            Untrigger(events, index, JumpUntriggers);
                            continue;
                        }
                    }

                    break;
                case "!combine":
                case "!target":
                case null or "":
                    break;
                default:
                    walkedEvents.Add(new WalkedEvent(regionIndex, position, ev.Copy(), false, index));
                    regionUsed = true;
                    break;
            }

            index++;
        }

        // The open region is always index regions.Count, so it has to be closed whenever
        // anything refers to it - a zero-length trailing region still holds the actions that
        // landed in it (a "!cut" right after a "!speed", say), and dropping it would strand them.
        if (position > 1e-9 || regionUsed || regions.Count == 0) regions.Add((speed, position));

        return new WalkedSequence(regions, walkedEvents, truncated);
    }

    private static double Scale(double current, BaseEvent ev)
    {
        return ev.ValueScale switch
        {
            ValueScale.Divide => current / ev.Value,
            ValueScale.Times => current * ev.Value,
            ValueScale.Add => current + ev.Value,
            _ => ev.Value
        };
    }

    /// <summary>
    ///     Ported from PlacementCalculator.Untrigger: re-arms loop/jump triggers from
    ///     <paramref name="index" /> onward (except the given event names) so nested loops can fire again.
    /// </summary>
    private static void Untrigger(BaseEvent[] events, int index, string[] except)
    {
        if (index == 0) index++;
        for (var i = index - 1; i < events.Length; i++)
        {
            var current = events[i];
            if (except.Contains(current.SoundEvent)) continue;
            current.Triggered = false;
            current.WorkingValue = current.Value;
        }
    }
}

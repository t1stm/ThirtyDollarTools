using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

/// <summary>
///     A maximal span of the timeline running at one grid rate ("!speed", in steps per minute).
/// </summary>
internal readonly record struct TempoRegion(double StartMinutes, double DurationMinutes, double Speed)
{
    public double EndMinutes => StartMinutes + DurationMinutes;
}

/// <summary>
///     Turns grid-placed notes into a sequential TDW sequence: one "!speed" per tempo region,
///     gaps as "!stop", same-step notes joined with "!combine". Both per-track playback and the
///     merged project export go through here, so they can never disagree on timing.
///     Fractional stops appear only where they are honest: automation timed in seconds, notes
///     overhanging onto a foreign grid, and concurrent tracks that share no common grid.
/// </summary>
internal static class SequenceBuilder
{
    /// <summary>
    ///     How far the merged export searches for a common grid when concurrent tracks run at
    ///     different rates (candidates are k * the fastest rate, k up to this). Past it the
    ///     fastest grid wins and the other tracks ride exact fractional stops.
    /// </summary>
    public const int MaxSpeedMultiplier = 64;

    public static bool SameSpeed(double a, double b)
    {
        return Math.Abs(a - b) <= 1e-9 * Math.Max(Math.Abs(a), Math.Abs(b));
    }

    public static Sequence Build(IReadOnlyList<TempoRegion> regions, (double Minutes, BaseEvent Event)[] timedEvents,
        SequenceStyle? style = null, IReadOnlyList<double>? barTimes = null)
    {
        var divider_every_bars = style?.DividerEveryBars ?? 0;
        var speed_dividers = style?.DividerOnSpeedChanges ?? false;
        var migrate_to_stop = style is null ? 1 : style.MigrateToStop; // null = "_pause" only

        var events = new List<BaseEvent>();
        var current_speed = double.NaN;
        var bar_index = 0;
        var bars_pending = 0; // bars completed since the last divider or tempo change

        // Assign each event to the region containing it and quantize the step to 6 decimals
        // (the serialized precision) so float noise can't split simultaneous events.
        var groups = timedEvents
            .Select(n =>
            {
                var region = RegionIndex(regions, n.Minutes);
                var step = Math.Round((n.Minutes - regions[region].StartMinutes) * regions[region].Speed, 6);
                return (Region: region, Step: step, n.Event);
            })
            .GroupBy(n => (n.Region, n.Step))
            .OrderBy(g => g.Key.Region).ThenBy(g => g.Key.Step)
            .Select(g => (g.Key.Region, g.Key.Step, Events: g.Select(n => n.Event).ToArray()))
            .ToArray();

        if (groups.Length == 0)
        {
            EmitSpeed(regions[0].Speed);
            return Finish(events);
        }

        var clock = 0d;
        var gi = 0;
        for (var ri = 0; ri < regions.Count && gi < groups.Length; ri++)
        {
            var region = regions[ri];
            // A one-step advance can overshoot a silent region shorter than the step.
            if (groups[gi].Region != ri && clock >= region.EndMinutes - 1e-12) continue;

            EmitDividersUpTo(region.StartMinutes);
            EmitSpeed(region.Speed);

            while (gi < groups.Length && groups[gi].Region == ri)
            {
                var (_, step, group_events) = groups[gi];
                var time = region.StartMinutes + step / region.Speed;

                EmitGap((time - clock) * region.Speed);

                // After the gap, before the notes: the silence belongs to the old bar,
                // the notes open the new one.
                EmitDividersUpTo(time);

                // Actions (e.g. "!cut") never advance the clock in playback, unlike sounds -
                // emit them first so they act on whatever's already playing before this
                // step's own sounds start, and join only consecutive *sounds* with
                // "!combine" (there is nothing to cancel around an action).
                var has_sound = false;
                var first_sound = true;
                foreach (var ev in group_events.Where(IsAction))
                    events.Add(ev);
                foreach (var ev in group_events.Where(ev => !IsAction(ev)))
                {
                    if (!first_sound) events.Add(Action("!combine", 0));
                    events.Add(ev);
                    first_sound = false;
                    has_sound = true;
                }

                gi++;
                if (!has_sound)
                {
                    // An actions-only group never consumes a step - matches PlacementCalculator,
                    // where actions never increment position. (Advancing here was the latent bug
                    // that shifted everything after an on-grid automation cut one step early.)
                    clock = time;
                }
                else
                {
                    // A sound advances the clock a whole step. When the next group comes sooner
                    // (an off-grid note), cancel the advance with "!combine" and let the next
                    // "!stop" carry the exact fractional gap instead.
                    var advance = 1d / region.Speed;
                    if (gi < groups.Length && GroupTime(gi) - time < advance * (1 - 1e-6))
                    {
                        events.Add(Action("!combine", 0));
                        clock = time;
                    }
                    else
                    {
                        clock = time + advance;
                    }
                }
            }

            if (gi >= groups.Length) break; // trailing silence isn't represented

            // Advance to the boundary so the next region starts on its own grid.
            EmitGap((region.EndMinutes - clock) * region.Speed);
            clock = Math.Max(clock, region.EndMinutes);
        }

        return Finish(events);

        // A gap becomes "_pause"s (one step each) below the migrate-to-stop threshold,
        // "!stop@n" at or above it. Fractional gaps always need "!stop" to stay exact.
        void EmitGap(double gap)
        {
            if (gap <= 1e-6) return;

            var steps = Math.Round(gap, 6);
            if (steps == Math.Round(steps) && (migrate_to_stop is not { } threshold || steps < threshold))
                for (var i = 0; i < (int)steps; i++)
                    events.Add(new NormalEvent { SoundEvent = "_pause" });
            else
                events.Add(Action("!stop", steps));
        }

        // Count bar lines as they pass; every "dividerEveryBars"-th one is a divider.
        // Several due bar lines inside one stretch of silence collapse to a single one.
        void EmitDividersUpTo(double minutes)
        {
            if (barTimes is null || divider_every_bars < 1) return;

            var due = false;
            while (bar_index < barTimes.Count && barTimes[bar_index] <= minutes + 1e-12)
            {
                bar_index++;
                if (++bars_pending < divider_every_bars) continue;
                bars_pending = 0;
                due = true;
            }

            if (due) EmitDivider();
        }

        void EmitSpeed(double speed)
        {
            if (!double.IsNaN(current_speed) && SameSpeed(current_speed, speed)) return;
            if (!double.IsNaN(current_speed))
            {
                if (speed_dividers) EmitDivider();
                bars_pending = 0; // a tempo change opens a new section: fresh bar count
            }

            events.Add(Action("!speed", speed));
            current_speed = speed;
        }

        double GroupTime(int index)
        {
            var (region, step, _) = groups[index];
            return regions[region].StartMinutes + step / regions[region].Speed;
        }

        void EmitDivider()
        {
            if (events.Count > 0 && events[^1].SoundEvent == "!divider") return;
            events.Add(Action("!divider", 0));
        }
    }

    /// <summary>
    ///     The last region starting at or before the given time - a note exactly on a
    ///     boundary belongs to the region that starts there (its downbeat).
    /// </summary>
    private static int RegionIndex(IReadOnlyList<TempoRegion> regions, double minutes)
    {
        int lo = 0, hi = regions.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (regions[mid].StartMinutes <= minutes + 1e-12) lo = mid;
            else hi = mid - 1;
        }

        return lo;
    }

    private static Sequence Finish(List<BaseEvent> events)
    {
        var sequence = new Sequence { Events = events.ToArray() };
        foreach (var ev in sequence.Events)
        {
            if (ev.SoundEvent is not null)
                sequence.UsedSounds.Add(ev.SoundEvent);

            // The encoder pre-allocates a mixer track per separated channel before
            // rendering (see PCMEncoder.GenerateAudioAndMixer) so a cut's target track
            // is guaranteed to exist - the text parser populates this as a side effect
            // of parsing "!cut@sound"; building the event list directly (not from text)
            // must populate it the same way, or cuts silently no-op against a track
            // that was never created.
            if (ev is IndividualCutEvent individualCut)
                foreach (var sound in individualCut.CutSounds)
                    sequence.SeparatedChannels.Add(sound);
        }

        return sequence;
    }

    /// <summary>Exactly PlacementCalculator's classification: actions never advance the clock.</summary>
    private static bool IsAction(BaseEvent ev)
    {
        return (ev.SoundEvent?.StartsWith('!') ?? true) || ev is ICustomActionEvent;
    }

    private static NormalEvent Action(string name, double value)
    {
        return new NormalEvent
        {
            SoundEvent = name,
            Value = value,
            WorkingValue = value,
            ValueScale = ValueScale.None
        };
    }
}
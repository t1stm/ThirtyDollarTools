using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

/// <summary>
///     A maximal span of the timeline running at one grid rate ("!speed", in steps per minute).
///     <paramref name="Bpm" /> is the quarter-note tempo that rate was built from, kept apart
///     from the grid resolution so the export can say "120 BPM, four steps a beat" instead of
///     the bare product. Zero means unknown (concurrent tracks at different tempi) - then the
///     rate is stated outright.
/// </summary>
internal readonly record struct TempoRegion(double StartMinutes, double DurationMinutes, double Speed, double Bpm = 0)
{
    public double EndMinutes => StartMinutes + DurationMinutes;

    /// <summary>How many grid steps fit in one beat: the "!speed@n@x" factor.</summary>
    public double Multiplier => Bpm > 0 ? Speed / Bpm : 1;
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
        var current_bpm = double.NaN;
        var current_multiplier = 1d;
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
            .Select(g => (g.Key.Region, g.Key.Step, Events: CollapseCuts(g.Select(n => n.Event).ToArray())))
            .ToArray();

        if (groups.Length == 0)
        {
            EmitSpeed(regions[0]);
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
            EmitSpeed(region);

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

        // The grid rate is the product of a tempo and a resolution, and the export says so:
        // "!speed@120|!speed@4@x" reads as "120 BPM, four steps a beat", where a bare
        // "!speed@480" reads as nothing at all. While the tempo holds, only what changed is
        // emitted - the ratio between the two resolutions - and a ratio that isn't a whole
        // factor either way (4 -> 6 steps) restates the pair instead, since the serialized
        // 6 decimals can't hold 1.333... exactly and the drift would compound.
        void EmitSpeed(TempoRegion region)
        {
            var first = double.IsNaN(current_speed);
            if (!first && SameSpeed(current_speed, region.Speed)) return;
            if (!first)
            {
                if (speed_dividers) EmitDivider();
                bars_pending = 0; // a tempo change opens a new section: fresh bar count
            }

            var bpm = region.Bpm > 0 ? region.Bpm : region.Speed;
            var multiplier = region.Multiplier;

            // Rounded, not raw: the factor the text carries and the one playback uses have
            // to be the same number, and 4/2 can land on 1.9999999999999998.
            var ratio = multiplier / current_multiplier;
            if (SameSpeed(current_bpm, bpm) && IsWhole(ratio))
            {
                events.Add(Action("!speed", Math.Round(ratio), ValueScale.Times));
            }
            else if (SameSpeed(current_bpm, bpm) && IsWhole(1 / ratio))
            {
                events.Add(Action("!speed", Math.Round(1 / ratio), ValueScale.Divide));
            }
            else
            {
                events.Add(Action("!speed", bpm));
                if (!SameSpeed(multiplier, 1)) events.Add(Action("!speed", multiplier, ValueScale.Times));
            }

            current_speed = region.Speed;
            current_bpm = bpm;
            current_multiplier = multiplier;

            // The opening tempo is a header, not music: a divider closes it off so the
            // first bar starts on a line of its own. Bar counting is untouched - this
            // isn't a bar line.
            if (first) EmitDivider();
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
    ///     All the cuts landing on one step collapse into a single one carrying the union of
    ///     their sounds. Tracks sharing an instrument (and several cut notes on the same step)
    ///     otherwise emit one cut per track per note, each re-silencing sounds the first cut
    ///     already silenced: identical audio, one extra encoder pass over every cut track.
    ///     Standard and legacy ("#icut") cuts stay separate - they serialize differently.
    /// </summary>
    private static BaseEvent[] CollapseCuts(BaseEvent[] events)
    {
        if (events.OfType<IndividualCutEvent>().Take(2).Count() < 2) return events;

        var merged = new Dictionary<bool, IndividualCutEvent>();
        var result = new List<BaseEvent>(events.Length);
        foreach (var ev in events)
        {
            if (ev is not IndividualCutEvent cut)
            {
                result.Add(ev);
                continue;
            }

            if (merged.TryGetValue(cut.IsStandardImplementation, out var existing))
            {
                existing.CutSounds.UnionWith(cut.CutSounds);
                continue;
            }

            // A fresh set, never the source event's: Copy() shares CutSounds by reference,
            // so unioning into an existing cut would edit whatever it was copied from.
            var collapsed = new IndividualCutEvent([.. cut.CutSounds], cut.IsStandardImplementation);
            merged[cut.IsStandardImplementation] = collapsed;
            result.Add(collapsed); // in place of the first cut: action order at a step is preserved
        }

        return result.ToArray();
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
        var sequence = new Sequence { Events = [.. events] };
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

    private static NormalEvent Action(string name, double value, ValueScale scale = ValueScale.None)
    {
        return new NormalEvent
        {
            SoundEvent = name,
            Value = value,
            WorkingValue = value,
            ValueScale = scale
        };
    }

    private static bool IsWhole(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 1e-9;
    }
}
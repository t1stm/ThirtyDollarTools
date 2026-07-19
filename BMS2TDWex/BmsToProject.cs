using ThirtyDollarConverter.Editor;

namespace BMS2TDW;

/// <summary>
///     Converts a parsed BMS chart into a ThirtyDollarConverter.Editor project:
///     one track per BMS channel group, one segment per constant-tempo span.
///     Everything in BMS is rational (positions are k/n of a measure, meters are
///     decimals, stops are 1/192ths of a whole note), so each measure gets a unit
///     grid of 1/L where L covers every division used in it - segments then use
///     one step per unit and the export reproduces the timeline exactly.
/// </summary>
public static class BmsToProject
{
    private const int BgmChannel = 1;

    public static ThirtyDollarProject Convert(BmsChart chart)
    {
        var project = new ThirtyDollarProject();
        project.Info.Name = chart.Title;
        project.Info.Author = chart.Artist;
        project.Info.Description = chart.Genre;
        project.RootTiming.BPM = (float)chart.Bpm;

        // All tracks need the full segment timeline from measure 0, so find the
        // used channels up front instead of creating tracks lazily.
        var channels = chart.Measures.Values
            .SelectMany(lines => lines)
            .Select(line => line.Channel)
            .Where(IsSoundChannel)
            .Distinct()
            .Order()
            .ToArray();

        var tracks = channels.ToDictionary(channel => channel, channel =>
        {
            var track = project.NewTrack();
            track.Name = channel == BgmChannel ? "BGM" : $"Lane {channel}";
            track.Segments[0].Numerator = 0; // zero-length placeholder; the generated timeline follows
            return track;
        });

        var max_measure = chart.Measures.Keys.DefaultIfEmpty(0).Max();
        var bpm = chart.Bpm;

        // The last tempo-span segment per track, for notes sitting exactly on a STOP:
        // they must sound before the pause, as an overhang on the span that just ended.
        var last_spans = channels.ToDictionary(channel => channel, _ => (TrackSegment?)null);

        for (var measure = 0; measure <= max_measure; measure++)
        {
            var lines = chart.Measures.GetValueOrDefault(measure) ?? [];
            var (p, q) = MeterFraction(chart.Meters.GetValueOrDefault(measure, 1m));

            // Unit grid: 1/L of the measure puts every object of every line on a whole
            // unit, and p | L keeps the segment denominators integral.
            var L = lines.Where(line => IsSoundChannel(line.Channel) || IsTempoChannel(line.Channel))
                .Aggregate(p, (acc, line) => Lcm(acc, line.Objects.Length));

            var bpm_changes = new Dictionary<long, double>();
            var stop_units = new Dictionary<long, double>(); // unit -> 1/192ths of a whole note
            foreach (var line in lines)
            foreach (var (unit, value) in Objects(line, L))
                switch (line.Channel)
                {
                    case 3:
                        bpm_changes[unit] = value;
                        break;
                    case 8:
                        if (chart.ExBpms.TryGetValue(value, out var ex)) bpm_changes[unit] = ex;
                        break;
                    case 9:
                        if (chart.Stops.TryGetValue(value, out var stop))
                            stop_units[unit] = stop_units.GetValueOrDefault(unit) + stop;
                        break;
                }

            var boundaries = bpm_changes.Keys.Concat(stop_units.Keys)
                .Concat([0L, L]).Distinct().Order().ToArray();

            // Build this measure's spans, identical timing for every track.
            var spans = new List<(long StartU, long EndU, Dictionary<int, TrackSegment> ByTrack)>();
            for (var i = 0; i < boundaries.Length - 1; i++)
            {
                var (start, end) = (boundaries[i], boundaries[i + 1]);

                if (bpm_changes.TryGetValue(start, out var new_bpm)) bpm = new_bpm;

                if (stop_units.TryGetValue(start, out var pause))
                    foreach (var track in tracks.Values)
                        AddSegment(track, (int)Math.Round(pause), 192, bpm);

                var by_track = tracks.ToDictionary(pair => pair.Key, pair =>
                    AddSegment(pair.Value, (int)(end - start), (int)(q * L / p), bpm));
                spans.Add((start, end, by_track));
            }

            // Place the sounds.
            foreach (var line in lines.Where(l => IsSoundChannel(l.Channel)))
            foreach (var (unit, value) in Objects(line, L))
            {
                if (!chart.Sounds.TryGetValue(value, out var sound)) continue;
                var track_channel = line.Channel;

                if (stop_units.ContainsKey(unit) &&
                    (unit > 0 ? FindSpan(spans, unit - 1).ByTrack[track_channel] : last_spans[track_channel]) is
                    { } previous)
                {
                    // Note exactly on a STOP: overhang on the span that just ended,
                    // so it sounds at the boundary, before the pause.
                    previous.Notes.Add(new Note { Step = previous.Numerator, Sound = sound });
                    continue;
                }

                var span = FindSpan(spans, unit);
                span.ByTrack[track_channel].Notes
                    .Add(new Note { Step = (int)(unit - span.StartU), Sound = sound });
            }

            foreach (var channel in channels)
                last_spans[channel] = spans[^1].ByTrack[channel];
        }

        // Only placed patterns sound; a BMS chart is one clip of every lane at time 0.
        var lane = 0;
        foreach (var track in project.Tracks) project.Place(track, lane++, 0);

        return project;
    }

    private static TrackSegment AddSegment(ProjectTrack track, int numerator, int denominator, double bpm)
    {
        var segment = track.NewSegment();
        segment.Numerator = numerator;
        segment.Denominator = denominator;
        segment.StepsPerBeat = 1;
        segment.BPM = (float)bpm;
        return segment;
    }

    private static (long StartU, long EndU, Dictionary<int, TrackSegment> ByTrack) FindSpan(
        List<(long StartU, long EndU, Dictionary<int, TrackSegment> ByTrack)> spans, long unit)
    {
        return spans.Last(span => span.StartU <= unit);
    }

    private static IEnumerable<(long Unit, int Value)> Objects(BmsLine line, long unitsPerMeasure)
    {
        for (var i = 0; i < line.Objects.Length; i++)
        {
            var value = line.Objects[i];
            if (value == 0) continue;
            yield return (i * unitsPerMeasure / line.Objects.Length, value);
        }
    }

    private static bool IsSoundChannel(int channel)
    {
        return channel is BgmChannel or >= 11 and <= 19 or >= 21 and <= 29;
    }

    private static bool IsTempoChannel(int channel)
    {
        return channel is 3 or 8 or 9;
    }

    private static (long P, long Q) MeterFraction(decimal meter)
    {
        long q = 1;
        while (meter != decimal.Truncate(meter))
        {
            meter *= 10;
            q *= 10;
        }

        var p = (long)meter;
        var gcd = Gcd(p, q);
        return (p / gcd, q / gcd);
    }

    private static long Lcm(long a, long b)
    {
        return a / Gcd(a, b) * b;
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }
}
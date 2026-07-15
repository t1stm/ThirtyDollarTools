using ThirtyDollarParser;

namespace ThirtyDollarConverter.Editor;

public class ThirtyDollarProject
{
    private readonly List<ProjectTrack> _projectTracks = [];
    private int _tracks;

    public ProjectInfo Info { get; set; } = new()
    {
        Name = "Untitled Project"
    };

    public TimingInfo RootTiming { get; set; } = new();
    public IReadOnlyList<ProjectTrack> Tracks => _projectTracks;

    public ProjectTrack NewTrack()
    {
        var track = new ProjectTrack(RootTiming, ++_tracks);
        _projectTracks.Add(track);
        return track;
    }

    /// <summary>
    ///     Reconstructs a track from a saved project, keeping the id counter ahead
    ///     of loaded ids. Null timing means the track follows the root timing.
    /// </summary>
    internal ProjectTrack AddTrack(int id, TimingInfo? timing)
    {
        var track = new ProjectTrack(timing ?? RootTiming, id);
        _projectTracks.Add(track);
        _tracks = Math.Max(_tracks, id);
        return track;
    }

    /// <summary>
    ///     Merges all tracks into a single TDW sequence for export, by absolute note time.
    ///     The timeline splits into tempo regions wherever any track changes grid rate and
    ///     each region exports at its own "!speed", so tempo changes never turn into stop
    ///     arithmetic. Timing is identical to per-track playback by construction.
    ///     Bar-based styling follows the first track's bar structure.
    /// </summary>
    public Sequence ToSequence(SequenceStyle? style = null)
    {
        var timed = _projectTracks.SelectMany(track => track.TimedNotes()).ToArray();
        var bar_times = _projectTracks.Count > 0 ? _projectTracks[0].BarTimes(style) : null;
        return SequenceBuilder.Build(MergedRegions(), timed, style, bar_times);
    }

    /// <summary>
    ///     Splits the timeline at every track's region boundaries. Concurrent differing
    ///     rates merge onto the smallest common grid up to
    ///     <see cref="SequenceBuilder.MaxSpeedMultiplier" /> x the fastest one; past that
    ///     the fastest grid wins and the other tracks ride exact fractional stops.
    /// </summary>
    private List<TempoRegion> MergedRegions()
    {
        var tracks = _projectTracks.Select(track => track.TempoRegions()).ToList();

        var bounds = tracks.SelectMany(regions => regions)
            .SelectMany(region => (double[])[region.StartMinutes, region.EndMinutes])
            .Order().ToArray();

        var merged = new List<TempoRegion>();
        var cursors = new int[tracks.Count];
        var rates = new List<double>();
        for (var i = 0; i + 1 < bounds.Length; i++)
        {
            var (start, end) = (bounds[i], bounds[i + 1]);
            if (end - start < 1e-10) continue;

            var mid = (start + end) / 2;
            rates.Clear();
            for (var t = 0; t < tracks.Count; t++)
            {
                var regions = tracks[t];
                while (cursors[t] < regions.Count && regions[cursors[t]].EndMinutes <= mid) cursors[t]++;
                if (cursors[t] >= regions.Count || regions[cursors[t]].StartMinutes > mid) continue;

                var rate = regions[cursors[t]].Speed;
                if (!rates.Any(r => SequenceBuilder.SameSpeed(r, rate))) rates.Add(rate);
            }

            if (rates.Count == 0) continue;

            var speed = CommonSpeed(rates);
            if (merged.Count > 0 && SequenceBuilder.SameSpeed(merged[^1].Speed, speed) &&
                merged[^1].EndMinutes >= start - 1e-10)
                merged[^1] = merged[^1] with { DurationMinutes = end - merged[^1].StartMinutes };
            else
                merged.Add(new TempoRegion(start, end - start, speed));
        }

        if (merged.Count == 0)
            merged.Add(new TempoRegion(0, 0, tracks.Count > 0 ? tracks[0][0].Speed : RootTiming.BPM));

        return merged;
    }

    private static double CommonSpeed(List<double> rates)
    {
        var fastest = rates.Max();
        for (var k = 1; k <= SequenceBuilder.MaxSpeedMultiplier; k++)
        {
            var candidate = k * fastest;
            var exact = rates.All(rate =>
            {
                var multiple = candidate / rate;
                return Math.Abs(multiple - Math.Round(multiple)) < 1e-7;
            });

            if (exact) return candidate;
        }

        return fastest;
    }
}
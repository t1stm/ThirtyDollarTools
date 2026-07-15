using ThirtyDollarParser;

namespace ThirtyDollarConverter.Editor;

public class ProjectTrack(TimingInfo timing, int id)
{
    private readonly List<TrackSegment> _segments = [new()];
    public int Id { get; set; } = id;
    public string Name { get; set; } = $"Track {id}";
    public TimingInfo Timing { get; set; } = timing;

    /// <summary>
    ///     The consecutive timing regions of this track, laid out back to back.
    ///     A track always holds at least one segment.
    /// </summary>
    public IReadOnlyList<TrackSegment> Segments => _segments;

    public TrackSegment NewSegment()
    {
        var segment = new TrackSegment();
        _segments.Add(segment);
        return segment;
    }

    /// <summary>
    ///     Removes a segment. Refuses when it is the track's last one.
    /// </summary>
    public bool RemoveSegment(TrackSegment segment)
    {
        return _segments.Count > 1 && _segments.Remove(segment);
    }

    /// <summary>
    ///     Converts only this track to a TDW sequence. Used for editor playback,
    ///     where each track gets its own AudioMixer channel.
    /// </summary>
    public Sequence ToSequence(SequenceStyle? style = null)
    {
        return SequenceBuilder.Build(TempoRegions(), TimedNotes().ToArray(), style, BarTimes(style));
    }

    /// <summary>
    ///     The absolute time of every bar line of this track, counted across segments.
    ///     Null when the style doesn't ask for bar dividers.
    /// </summary>
    internal double[]? BarTimes(SequenceStyle? style)
    {
        if (style?.DividerEveryBars is not { } every || every < 1) return null;

        var times = new List<double>();
        var offset = 0d;
        foreach (var segment in _segments)
        {
            var bar_minutes = segment.Numerator * segment.StepsPerBeat * segment.StepMinutes(Timing.BPM);
            if (bar_minutes <= 0) continue; // zero-length placeholder segments have no bar lines
            for (var b = 0; b < segment.Bars; b++)
            {
                offset += bar_minutes;
                times.Add(offset);
            }
        }

        return times.ToArray();
    }

    /// <summary>
    ///     Every note of this track with its absolute time. Segments inherit the track's
    ///     BPM; their own time signature and resolution set the local step length.
    /// </summary>
    internal IEnumerable<(double Minutes, Note Note)> TimedNotes()
    {
        var offset = 0d;
        foreach (var segment in _segments)
        {
            var step_minutes = segment.StepMinutes(Timing.BPM);
            foreach (var note in segment.Notes)
            {
                var minutes = offset + note.Step * step_minutes;
                yield return (minutes, note);

                if (note.Automation is null) continue;
                foreach (var generated in note.Automation.Expand(note, minutes, step_minutes))
                    yield return generated;
            }

            offset += segment.DurationMinutes(Timing.BPM);
        }
    }

    /// <summary>
    ///     The track's timeline as tempo regions: consecutive segments with equal grid
    ///     rates merged into one. "!speed" changes exactly at region boundaries.
    /// </summary>
    internal List<TempoRegion> TempoRegions()
    {
        var regions = new List<TempoRegion>();
        var offset = 0d;
        foreach (var segment in _segments)
        {
            var duration = segment.DurationMinutes(Timing.BPM);
            if (duration > 0)
            {
                var rate = 1d / segment.StepMinutes(Timing.BPM);
                if (regions.Count > 0 && SequenceBuilder.SameSpeed(regions[^1].Speed, rate))
                    regions[^1] = regions[^1] with { DurationMinutes = regions[^1].DurationMinutes + duration };
                else
                    regions.Add(new TempoRegion(offset, duration, rate));
            }

            offset += duration;
        }

        if (regions.Count == 0) // only zero-length segments: no timeline, just a grid rate
            regions.Add(new TempoRegion(0, 0, 1d / _segments[0].StepMinutes(Timing.BPM)));

        return regions;
    }
}
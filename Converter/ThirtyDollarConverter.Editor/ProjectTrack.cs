using ThirtyDollarParser;

namespace ThirtyDollarConverter.Editor;

public class ProjectTrack(TimingInfo timing, int id)
{
    private readonly List<TrackSegment> _segments = [new()];
    private readonly List<TrackAutomation> _trackAutomations = [];
    public int Id { get; set; } = id;
    public string Name { get; set; } = $"Track {id}";
    public TimingInfo Timing { get; set; } = timing;

    /// <summary>
    ///     The consecutive timing regions of this track, laid out back to back.
    ///     A track always holds at least one segment.
    /// </summary>
    public IReadOnlyList<TrackSegment> Segments => _segments;

    /// <summary>
    ///     Automations that apply to every matching note in the track (filtered by sound,
    ///     or every sound), rather than one note's own <see cref="Note.Automation" />.
    /// </summary>
    public IReadOnlyList<TrackAutomation> TrackAutomations => _trackAutomations;

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
    ///     Deep copy for track duplication: fresh segments/notes/automations, so editing the
    ///     copy can never reach the source. Placements aren't copied — a duplicated pattern
    ///     starts unplaced, like any newly added track.
    /// </summary>
    internal ProjectTrack Duplicate(int id, string name)
    {
        var copy = new ProjectTrack(Timing, id) { Name = name };
        copy._segments.Clear();
        foreach (var segment in _segments)
            copy._segments.Add(segment.Duplicate());
        foreach (var automation in _trackAutomations)
            copy._trackAutomations.Add(new TrackAutomation
            {
                Keyframes = automation.Keyframes.Clone(),
                Sounds = automation.Sounds?.ToList()
            });
        return copy;
    }

    public TrackAutomation AddTrackAutomation(AudioKeyframeManager keyframes, List<string>? sounds = null)
    {
        var automation = new TrackAutomation { Keyframes = keyframes, Sounds = sounds };
        _trackAutomations.Add(automation);
        return automation;
    }

    public void RemoveTrackAutomation(TrackAutomation automation)
    {
        _trackAutomations.Remove(automation);
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
    ///     Total pattern length in minutes — the clip width on the arrangement grid.
    /// </summary>
    public double DurationMinutes()
    {
        return _segments.Sum(segment => segment.DurationMinutes(Timing.BPM));
    }

    /// <summary>
    ///     Continuous grid-step position at the given time since the track's start, in the
    ///     note editor's coordinate space where segments pack back to back by step count
    ///     rather than real time. Negative before the track starts (proportional, from the
    ///     first segment's step rate); past the last segment's total step count once the
    ///     track has finished. Used to place the playback playhead in the note editor.
    /// </summary>
    public double StepPositionAt(double minutes)
    {
        var elapsed = 0d;
        var steps = 0d;
        foreach (var segment in _segments)
        {
            var duration = segment.DurationMinutes(Timing.BPM);
            if (duration <= 0) continue;
            if (minutes < elapsed + duration)
                return steps + (minutes - elapsed) / segment.StepMinutes(Timing.BPM);

            elapsed += duration;
            steps += segment.StepCount;
        }

        return steps;
    }

    /// <summary>
    ///     Inverse of <see cref="StepPositionAt" />: the elapsed time at a given continuous
    ///     grid-step position. Used to convert a note editor ruler click back into a time.
    /// </summary>
    public double MinutesAtStepPosition(double steps)
    {
        var elapsed = 0d;
        var stepsAccum = 0d;
        foreach (var segment in _segments)
        {
            if (segment.StepCount <= 0) continue;
            if (steps < stepsAccum + segment.StepCount)
                return elapsed + (steps - stepsAccum) * segment.StepMinutes(Timing.BPM);

            elapsed += segment.DurationMinutes(Timing.BPM);
            stepsAccum += segment.StepCount;
        }

        return elapsed;
    }

    /// <summary>
    ///     The absolute time of every bar line of this track, counted across segments.
    ///     Null when the style doesn't ask for bar dividers.
    /// </summary>
    internal double[]? BarTimes(SequenceStyle? style, double startMinutes = 0)
    {
        if (style?.DividerEveryBars is not { } every || every < 1) return null;

        var times = new List<double>();
        var offset = startMinutes;
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
    ///     Every sound event of this track (notes and their generated automation, flattened
    ///     to instrument sounds) with its absolute time. Segments inherit the track's BPM;
    ///     their own time signature and resolution set the local step length.
    /// </summary>
    internal IEnumerable<(double Minutes, BaseEvent Event)> TimedNotes(double startMinutes = 0)
    {
        var offset = startMinutes;
        foreach (var segment in _segments)
        {
            var step_minutes = segment.StepMinutes(Timing.BPM);
            foreach (var note in segment.Notes)
            {
                var minutes = offset + note.Step * step_minutes;
                foreach (var ev in note.ToEvents())
                    yield return (minutes, ev);

                if (note.Automation is not null)
                    foreach (var generated in note.Automation.Expand(note, minutes, step_minutes))
                        yield return generated;

                foreach (var automation in _trackAutomations)
                {
                    if (automation.Sounds is { } sounds && !note.Instrument.Sounds.Any(sounds.Contains)) continue;
                    foreach (var generated in automation.Keyframes.Expand(note, minutes, step_minutes))
                        yield return generated;
                }
            }

            offset += segment.DurationMinutes(Timing.BPM);
        }
    }

    /// <summary>
    ///     The track's timeline as tempo regions: consecutive segments with equal grid
    ///     rates merged into one. "!speed" changes exactly at region boundaries.
    /// </summary>
    internal List<TempoRegion> TempoRegions(double startMinutes = 0)
    {
        var regions = new List<TempoRegion>();
        var offset = startMinutes;
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
            regions.Add(new TempoRegion(startMinutes, 0, 1d / _segments[0].StepMinutes(Timing.BPM)));

        return regions;
    }
}
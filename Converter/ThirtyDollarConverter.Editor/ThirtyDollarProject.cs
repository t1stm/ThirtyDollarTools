using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

namespace ThirtyDollarConverter.Editor;

public class ThirtyDollarProject
{
    private readonly List<TrackPlacement> _placements = [];
    private readonly List<ProjectTrack> _projectTracks = [];
    private readonly List<Instrument> _instruments = [];
    private int _tracks;
    private int _instrumentIds;

    public ProjectInfo Info { get; set; } = new()
    {
        Name = "Untitled Project"
    };

    public TimingInfo RootTiming { get; set; } = new();

    /// <summary>
    ///     Project-wide pitch shift in semitones. Falls back for any track whose own
    ///     <see cref="ProjectTrack.Transpose" /> is null.
    /// </summary>
    public float Transpose { get; set; }
    public IReadOnlyList<ProjectTrack> Tracks => _projectTracks;
    public IReadOnlyList<Instrument> Instruments => _instruments;

    /// <summary>
    ///     The arrangement: clips of patterns on channels. Only placed patterns sound -
    ///     a track without placements is silent, FL-style.
    /// </summary>
    public IReadOnlyList<TrackPlacement> Placements => _placements;

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

    /// <summary>Duplicates a track under a fresh id, with all its segments/notes/automations deep-copied.</summary>
    public ProjectTrack DuplicateTrack(ProjectTrack track, string name)
    {
        var copy = track.Duplicate(++_tracks, name);
        _projectTracks.Add(copy);
        return copy;
    }

    public bool RemoveTrack(ProjectTrack track)
    {
        if (!_projectTracks.Remove(track)) return false;
        _placements.RemoveAll(placement => placement.Track == track);
        return true;
    }

    /// <summary>Re-inserts a track removed earlier (e.g. by undo), preserving its identity and position.</summary>
    public void AddTrack(ProjectTrack track, int index)
    {
        _projectTracks.Insert(Math.Clamp(index, 0, _projectTracks.Count), track);
    }

    public Instrument NewInstrument(string name)
    {
        var instrument = new Instrument { Id = ++_instrumentIds, Name = name };
        _instruments.Add(instrument);
        return instrument;
    }

    /// <summary>
    ///     Reconstructs an instrument from a saved project, keeping the id counter ahead
    ///     of loaded ids.
    /// </summary>
    internal Instrument AddInstrument(int id, string name)
    {
        var instrument = new Instrument { Id = id, Name = name };
        _instruments.Add(instrument);
        _instrumentIds = Math.Max(_instrumentIds, id);
        return instrument;
    }

    /// <summary>Refuses while any note still references the instrument.</summary>
    public bool RemoveInstrument(Instrument instrument)
    {
        var referenced = _projectTracks
            .SelectMany(track => track.Segments)
            .SelectMany(segment => segment.Notes)
            .Any(note => note.Instrument == instrument);
        return !referenced && _instruments.Remove(instrument);
    }

    /// <summary>Re-inserts an instrument removed earlier (e.g. by undo), preserving its identity and position.</summary>
    public void AddInstrument(Instrument instrument, int index)
    {
        _instruments.Insert(Math.Clamp(index, 0, _instruments.Count), instrument);
    }

    /// <summary>
    ///     Migration helper: the single-sound instrument named after <paramref name="sound" />,
    ///     creating and registering one if none exists yet. Dedups pre-instrument files where
    ///     every note held a bare sound name, so repeated uses of one sound share an instrument.
    /// </summary>
    internal Instrument GetOrCreateInstrument(string sound)
    {
        var existing = _instruments.FirstOrDefault(instrument => instrument.Sounds is [{ Sound: var only }] && only == sound);
        if (existing is not null) return existing;

        var instrument = NewInstrument(sound);
        instrument.AddSound(sound);
        return instrument;
    }

    public TrackPlacement Place(ProjectTrack track, int channel, double startQuarterNotes)
    {
        if (!_projectTracks.Contains(track))
            throw new ArgumentException("The track does not belong to this project.", nameof(track));

        var placement = new TrackPlacement(track, channel, startQuarterNotes);
        _placements.Add(placement);
        return placement;
    }

    public bool RemovePlacement(TrackPlacement placement)
    {
        return _placements.Remove(placement);
    }

    /// <summary>Re-adds a placement removed earlier (e.g. by undo), preserving its identity.</summary>
    public void AddPlacement(TrackPlacement placement)
    {
        _placements.Add(placement);
    }

    /// <summary>Where a clip starts in absolute time: quarter notes at the root BPM.</summary>
    internal double StartMinutes(TrackPlacement placement)
    {
        return placement.StartQuarterNotes / RootTiming.BPM;
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
        return BuildSequence(_placements, style);
    }

    /// <summary>
    ///     The merged sequence restricted to placements on audible channels - editor
    ///     playback's render unit. A muted channel's cuts still apply, via the same
    ///     cross-channel injection <see cref="BuildSequence" /> uses for
    ///     <see cref="ChannelSequence" />.
    /// </summary>
    public Sequence ToSequence(Func<int, bool> isChannelAudible, SequenceStyle? style = null)
    {
        return BuildSequence(_placements.Where(p => isChannelAudible(p.Channel)).ToList(), style);
    }

    /// <summary>
    ///     The sequence of a single arrangement channel - the unit of editor playback,
    ///     where every lane renders separately and the enabled lanes are mixed. Anchored
    ///     on the same timeline origin as <see cref="ToSequence" /> so the per-channel
    ///     renders stay sample-aligned with each other and with the merged export.
    /// </summary>
    public Sequence ChannelSequence(int channel, SequenceStyle? style = null)
    {
        return BuildSequence(_placements.Where(p => p.Channel == channel).ToList(), style);
    }

    private Sequence BuildSequence(List<TrackPlacement> placements, SequenceStyle? style)
    {
        var timed = placements
            .SelectMany(placement => placement.Track.TimedNotes(StartMinutes(placement), Transpose))
            .ToList();

        // Cross-channel cut parity: real (merged) playback has a cut silence everything
        // currently playing project-wide, but editor playback renders each channel in
        // isolation. Inject every OTHER placement's cuts (harmless no-op when building
        // the full merged export, where there is no "outside") so an isolated channel's
        // preview matches the export instead of missing cuts from other channels' tracks.
        foreach (var placement in _placements.Except(placements))
            timed.AddRange(placement.Track.TimedNotes(StartMinutes(placement), Transpose)
                .Where(t => t.Event is IndividualCutEvent));

        var bar_times = placements.Count > 0
            ? placements[0].Track.BarTimes(style, StartMinutes(placements[0]))
            : null;
        return SequenceBuilder.Build(MergedRegions(placements), timed.ToArray(), style, bar_times);
    }

    /// <summary>
    ///     Splits the timeline at every track's region boundaries. Concurrent differing
    ///     rates merge onto the smallest common grid up to
    ///     <see cref="SequenceBuilder.MaxSpeedMultiplier" /> x the fastest one; past that
    ///     the fastest grid wins and the other tracks ride exact fractional stops.
    /// </summary>
    private List<TempoRegion> MergedRegions(List<TrackPlacement> placements)
    {
        var tracks = placements
            .Select(placement => placement.Track.TempoRegions(StartMinutes(placement)))
            .ToList();

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

            if (rates.Count == 0)
            {
                // Silence between placements is real time: extend the previous region
                // so the timeline stays continuous (Build emits its gap as a stop tail).
                // Silence before the first placement is dropped, like trailing silence.
                if (merged.Count > 0)
                    merged[^1] = merged[^1] with { DurationMinutes = end - merged[^1].StartMinutes };
                continue;
            }

            var speed = CommonSpeed(rates);
            if (merged.Count > 0 && SequenceBuilder.SameSpeed(merged[^1].Speed, speed) &&
                merged[^1].EndMinutes >= start - 1e-10)
                merged[^1] = merged[^1] with { DurationMinutes = end - merged[^1].StartMinutes };
            else
                merged.Add(new TempoRegion(start, end - start, speed));
        }

        if (merged.Count == 0)
            merged.Add(new TempoRegion(0, 0,
                tracks.Count > 0 ? tracks[0][0].Speed : RootTiming.BPM));

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
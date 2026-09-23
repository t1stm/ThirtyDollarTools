using ThirtyDollarConverter.Editor;

namespace EditorScene.State;

/// <summary>
///     One track's segments, walked once: which segment holds a note, and the same
///     (segment, local step) / track-absolute step mapping as
///     <see cref="ProjectTrack.GlobalStepOf(TrackSegment, int)" /> and
///     <see cref="ProjectTrack.SegmentAtGlobalStep" />, answered by lookup rather than by a
///     walk over every segment. A gesture over the selection asks once per note, and on a
///     track of thousands of segments those walks made a select-all drag cost ~70 ms a frame.
///     A snapshot: build a fresh one once the segments or their notes have changed.
/// </summary>
internal sealed class TrackLayout
{
    /// <summary>Where each segment ends, in track-absolute steps. Never decreasing - a segment holds no fewer than zero steps.</summary>
    private readonly int[] _ends;

    private readonly TrackSegment[] _segments;
    private readonly Dictionary<TrackSegment, int> _starts = [];
    private Dictionary<Note, TrackSegment>? _homes;

    public TrackLayout(ProjectTrack track)
    {
        _segments = [.. track.Segments];
        _ends = new int[_segments.Length];

        var offset = 0;
        for (var i = 0; i < _segments.Length; i++)
        {
            _starts.TryAdd(_segments[i], offset);
            offset += _segments[i].StepCount;
            _ends[i] = offset;
        }

        TotalSteps = offset;
    }

    /// <summary>Every segment's steps together: the track's length on the grid.</summary>
    public int TotalSteps { get; }

    /// <inheritdoc cref="ProjectTrack.GlobalStepOf(TrackSegment, int)" />
    public int GlobalStepOf(TrackSegment segment, int localStep)
    {
        return _starts.TryGetValue(segment, out var start) ? start + localStep : localStep;
    }

    /// <inheritdoc cref="ProjectTrack.SegmentAtGlobalStep" />
    public (TrackSegment Segment, int LocalStep)? SegmentAt(int globalStep)
    {
        // The first segment that ends past the step, which passes over zero-length segments
        // just as the walk does.
        int first = 0, last = _ends.Length;
        while (first < last)
        {
            var middle = (first + last) / 2;
            if (_ends[middle] <= globalStep) first = middle + 1;
            else last = middle;
        }

        if (first == _ends.Length) return null;
        return (_segments[first], globalStep - (first == 0 ? 0 : _ends[first - 1]));
    }

    /// <summary>The segment holding the note, or null when none of them does.</summary>
    public TrackSegment? SegmentOf(Note note)
    {
        if (_homes is null)
        {
            _homes = [];
            foreach (var segment in _segments)
            foreach (var held in segment.Notes)
                _homes.TryAdd(held, segment);
        }

        return _homes.GetValueOrDefault(note);
    }
}

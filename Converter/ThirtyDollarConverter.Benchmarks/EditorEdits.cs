using ThirtyDollarConverter.Editor;

namespace ThirtyDollarConverter.Benchmarks;

/// <summary>
///     The edits an editor session actually makes, applied to a real cut-heavy project. Every
///     one of them is cyclic - invocation <c>n</c> leaves the
///     project in a state the next invocation edits again, so a benchmark can run thousands of
///     them without rebuilding a baseline, exactly like holding a scroll wheel down over a note.
/// </summary>
public enum EditKind
{
    /// <summary>
    ///     Scroll a note's value. The note carries cut automation - the hardest case for an
    ///     incremental render.
    /// </summary>
    AutomatedNoteValue,

    /// <summary>Scroll a note's volume, same automated note.</summary>
    AutomatedNoteVolume,

    /// <summary>Scroll the value of a note with no automation of its own.</summary>
    PlainNoteValue,

    /// <summary>Drag a note one step left and back.</summary>
    MoveNote,

    /// <summary>Place a note and take it away again.</summary>
    AddRemoveNote,

    /// <summary>Retune every note of one instrument - a broad edit that dirties the whole song.</summary>
    RetuneInstrument
}

public sealed class EditorEdits
{
    private readonly Note _automated;
    private readonly Instrument _instrument;
    private readonly Note _movable;
    private readonly Note _plain;
    private readonly TrackSegment _segment;
    private readonly Note _spare;

    public EditorEdits(ThirtyDollarProject project)
    {
        Project = project;

        var notes = project.Tracks
            .SelectMany(track => track.Segments.Select(segment => (track, segment)))
            .SelectMany(pair => pair.segment.Notes.Select(note => (pair.track, pair.segment, note)))
            .ToList();

        var automated = notes.FirstOrDefault(entry => entry.note.Automation is { Keyframes.Count: > 0 } automation
                                                      && automation.Keyframes.Any(keyframe => keyframe.Cut));
        if (automated.note == null) automated = notes.First(entry => entry.note.Automation != null);

        _automated = automated.note;
        _plain = notes.FirstOrDefault(entry => entry.note.Automation == null).note ?? notes[^1].note;
        _movable = notes[notes.Count / 2].note;
        _segment = notes[notes.Count / 2].segment;
        _spare = new Note { Step = _movable.Step + 1, Instrument = _movable.Instrument };
        _instrument = project.Instruments.OrderByDescending(instrument =>
            notes.Count(entry => entry.note.Instrument == instrument)).First();
    }

    public ThirtyDollarProject Project { get; }

    /// <summary>Applies edit number <paramref name="step" /> of the given kind.</summary>
    public void Apply(EditKind kind, int step)
    {
        switch (kind)
        {
            case EditKind.AutomatedNoteValue:
                _automated.Value = step % 5 - 2;
                break;

            case EditKind.AutomatedNoteVolume:
                _automated.Volume = 40 + step % 5 * 10;
                break;

            case EditKind.PlainNoteValue:
                _plain.Value = step % 5 - 2;
                break;

            case EditKind.MoveNote:
                _movable.Step += step % 2 == 0 ? 1 : -1;
                break;

            case EditKind.AddRemoveNote:
                if (step % 2 == 0) _segment.Notes.Add(_spare);
                else _segment.Notes.Remove(_spare);
                break;

            case EditKind.RetuneInstrument:
                foreach (var sound in _instrument.Sounds) sound.Value = step % 3 - 1;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
}
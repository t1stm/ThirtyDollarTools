using ThirtyDollarConverter.Editor;

namespace EditorScene.State;

/// <summary>
///     The project's instrument library, and which instrument a click in the note editor places.
/// </summary>
public partial class EditorState
{
    private readonly Dictionary<ProjectTrack, Instrument?> _lastInstrumentByTrack = [];

    /// <summary>
    ///     The instrument a click in the note editor places. Session-only, never saved.
    ///     Remembered per track (see <see cref="OpenTrack" />), so switching back to a track
    ///     restores whichever instrument was last active in it.
    /// </summary>
    public Instrument? ActiveInstrument
    {
        get;
        set
        {
            field = value;
            if (OpenedTrack is { } track) _lastInstrumentByTrack[track] = value;
        }
    }

    /// <summary>
    ///     Raises <see cref="OnInstrumentsChanged" /> for callers outside this class - a
    ///     plain <see cref="ActiveInstrument" /> set fires no event of its own, but the
    ///     active-instrument button still needs to refresh (see
    ///     <see cref="Scenes.Components.InstrumentWorkflow" />'s pick flow).
    /// </summary>
    public void NotifyInstrumentsChanged()
    {
        OnInstrumentsChanged?.Invoke();
    }

    public Instrument AddInstrument(string name)
    {
        var instrument = Project.NewInstrument(name);
        Touch();
        OnInstrumentsChanged?.Invoke();
        return instrument;
    }

    public void RenameInstrument(Instrument instrument, string name)
    {
        if (instrument.Name == name) return;
        instrument.Name = name;
        Touch();
        OnInstrumentsChanged?.Invoke();
    }

    /// <summary>
    ///     Replaces an instrument's sounds. The instances are cloned - the editor's
    ///     picker keeps mutating its own as the user scrolls them.
    /// </summary>
    public void SetInstrumentSounds(Instrument instrument, IEnumerable<InstrumentSound> sounds)
    {
        instrument.Sounds.Clear();
        instrument.Sounds.AddRange(sounds.Select(sound => sound.Clone()));

        Touch();
        OnInstrumentsChanged?.Invoke();
    }

    /// <summary>Refuses while any note still references the instrument (library invariant).</summary>
    public bool RemoveInstrument(Instrument instrument)
    {
        if (!Project.RemoveInstrument(instrument)) return false;
        if (ActiveInstrument == instrument)
            ActiveInstrument = Project.Instruments.Count > 0 ? Project.Instruments[0] : null;
        Touch();
        OnInstrumentsChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     Deletes the instrument outright: every note using it, in every track, is removed
    ///     first so <see cref="RemoveInstrument" />'s reference guard never refuses. Used by
    ///     the explicit "delete instrument" action (after user confirmation), as opposed to
    ///     <see cref="RemoveInstrument" />'s safe default.
    /// </summary>
    public void DeleteInstrumentEverywhere(Instrument instrument)
    {
        var removedNotes = new List<(TrackSegment Segment, Note Note, int Index)>();
        foreach (var segment in Project.Tracks.SelectMany(track => track.Segments))
            for (var i = segment.Notes.Count - 1; i >= 0; i--)
            {
                if (segment.Notes[i].Instrument != instrument) continue;
                removedNotes.Add((segment, segment.Notes[i], i));
                segment.Notes.RemoveAt(i);
            }

        removedNotes.Reverse();

        _notes.Remove(removedNotes.Select(removed => removed.Note));

        var instrumentIndex = IndexOf(Project.Instruments, instrument);
        _undoHistory.Push(
            () =>
            {
                Project.AddInstrument(instrument, instrumentIndex);
                foreach (var (segment, note, index) in removedNotes)
                    segment.Notes.Insert(Math.Clamp(index, 0, segment.Notes.Count), note);
            },
            () =>
            {
                foreach (var (segment, note, _) in removedNotes) segment.Notes.Remove(note);
                RemoveInstrument(instrument);
            });

        RemoveInstrument(instrument);
    }

    /// <summary>The instruments a track plays, in track order - both kinds hold them differently.</summary>
    private static IEnumerable<Instrument> InstrumentsOf(ProjectTrack track)
    {
        return track is FaithfulTrack faithful
            ? faithful.Items.Select(item => item.Note?.Instrument).OfType<Instrument>()
            : track.Segments.SelectMany(segment => segment.Notes).Select(note => note.Instrument);
    }
}

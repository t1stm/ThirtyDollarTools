using ThirtyDollarConverter.Editor;

namespace EditorScene.State;

/// <summary>
///     The piano-roll side: a track's segments, the notes inside them, and moving or nudging
///     a whole note selection at once.
/// </summary>
public partial class EditorState
{
    public TrackSegment? SelectedSegment { get; private set; }

    public void SelectSegment(TrackSegment? segment)
    {
        if (SelectedSegment == segment) return;
        SelectedSegment = segment;
        OnSegmentSelectionChanged?.Invoke(segment);
    }

    public TrackSegment AddSegment(ProjectTrack track)
    {
        var segment = track.NewSegment();
        Touch();
        return segment;
    }

    /// <summary>Removes a segment; refuses on the track's last one (library invariant).</summary>
    public bool RemoveSegment(ProjectTrack track, TrackSegment segment)
    {
        var index = IndexOf(track.Segments, segment);
        if (!track.RemoveSegment(segment)) return false;
        _notes.Remove(segment.Notes);
        if (SelectedSegment == segment) SelectSegment(track.Segments[0]);

        _undoHistory.Push(
            () => track.AddSegment(segment, index),
            () => track.RemoveSegment(segment));
        Touch();
        return true;
    }

    public Note AddNote(TrackSegment segment, int step, Instrument instrument, double value, bool isCut = false)
    {
        // A cut note always has default Volume/Pan/Offset/Automation (see Note.IsCut) -
        // copied modifiers from a previous pick never apply to it.
        var modifiers = isCut ? null : CopiedModifiers;
        var note = new Note
        {
            Step = step,
            Instrument = instrument,
            Value = value,
            Volume = modifiers?.Volume,
            Pan = modifiers?.Pan ?? 0,
            Offset = modifiers?.Offset ?? 0,
            Automation = modifiers?.Automation?.Clone(),
            IsCut = isCut
        };
        segment.Notes.Add(note);
        _undoHistory.PushInsert(segment.Notes, note, segment.Notes.Count - 1);
        Touch();
        return note;
    }

    public bool RemoveNote(TrackSegment segment, Note note)
    {
        var index = segment.Notes.IndexOf(note);
        if (index < 0) return false;

        segment.Notes.RemoveAt(index);
        _notes.Remove([note]);
        _undoHistory.PushRemove(segment.Notes, note, index);
        Touch();
        return true;
    }

    public void MoveNote(TrackSegment from, TrackSegment to, Note note, int step, double value)
    {
        if (from == to && note.Step == step && note.Value == value) return;
        var (prevSegment, prevStep, prevValue) = (from, note.Step, note.Value);

        if (from != to)
        {
            from.Notes.Remove(note);
            to.Notes.Add(note);
        }

        note.Step = step;
        note.Value = value;

        _undoHistory.PushOrMergeMove(note,
            () =>
            {
                if (to != prevSegment)
                {
                    to.Notes.Remove(note);
                    prevSegment.Notes.Add(note);
                }

                note.Step = prevStep;
                note.Value = prevValue;
            },
            () =>
            {
                if (prevSegment != to)
                {
                    prevSegment.Notes.Remove(note);
                    to.Notes.Add(note);
                }

                note.Step = step;
                note.Value = value;
            });
        Touch();
    }

    /// <summary>
    ///     Moves every given note to its target (segment, step, value) together, as one shared
    ///     drag gesture - dragging any note of a selection moves the whole group. Every drag
    ///     frame calls this again with the group's latest targets;
    ///     <see cref="BeginGesture" /> + <see cref="UndoHistory.PushOrMergeMove" />, keyed on
    ///     the first note in the list, collapse the run into one undo entry.
    /// </summary>
    public void MoveSelectedNotes(ProjectTrack track,
        IReadOnlyList<(Note Note, TrackSegment Segment, int Step, double Value, float? End)> targets)
    {
        if (targets.Count == 0) return;

        var before = targets
            .Select(t => (t.Note, Segment: FindSegment(track, t.Note), t.Note.Step, t.Note.Value,
                End: t.Note.Automation?.End))
            .ToArray();
        var changed = false;
        for (var i = 0; i < targets.Count; i++)
        {
            var (note, segment, step, value, end) = targets[i];
            if (before[i].Segment != segment || note.Step != step || note.Value != value ||
                before[i].End != end) changed = true;
        }

        if (!changed) return;

        Apply(track, targets);

        _undoHistory.PushOrMergeMove(targets[0].Note,
            () => Apply(track, before),
            () => Apply(track, targets));
        Touch();
    }

    /// <summary>
    ///     One frame of an automation gesture - a keyframe marker drag. The whole automation
    ///     is snapshotted around the edit rather than each field plumbed through: a drag
    ///     touches a keyframe's value, its position and sometimes the template, and
    ///     <see cref="BeginGesture" /> + <see cref="UndoHistory.PushOrMergeMove" /> keyed on
    ///     the manager collapse the run of frames into one entry either way.
    /// </summary>
    public void EditAutomation(AudioKeyframeManager automation, Action edit)
    {
        var before = automation.Clone();
        edit();
        if (automation.ValueEquals(before)) return;

        var after = automation.Clone();
        _undoHistory.PushOrMergeMove(automation,
            () => automation.CopyFrom(before),
            () => automation.CopyFrom(after));
        Touch();
    }

    /// <summary>
    ///     Arrow-key nudge of the selected notes: whole steps along the track and values
    ///     up/down, clamped the same way a drag is (<paramref name="maxValue" /> is the
    ///     view's value range). One <see cref="BeginGesture" /> per call, so a keystroke is
    ///     its own undo entry rather than merging into the drag before it.
    /// </summary>
    public void NudgeNotes(int stepDelta, double valueDelta, double maxValue)
    {
        if (OpenedTrack is not { } track || _notes.Count == 0) return;

        var maxGlobalStep = Math.Max(0, track.Segments.Sum(segment => segment.StepCount) - 1);
        var targets = new List<(Note Note, TrackSegment Segment, int Step, double Value, float? End)>(_notes.Count);
        foreach (var note in _notes.Items)
        {
            var globalStep = Math.Clamp(
                track.GlobalStepOf(FindSegment(track, note), note.Step) + stepDelta, 0, maxGlobalStep);
            if (track.SegmentAtGlobalStep(globalStep) is not { } mapped) continue;

            targets.Add((note, mapped.Segment, mapped.LocalStep,
                Math.Clamp(note.Value + valueDelta, -maxValue, maxValue), note.Automation?.End));
        }

        BeginGesture();
        MoveSelectedNotes(track, targets);
    }

    /// <summary>
    ///     Places every note at its target (segment, step, value, length), first removing it
    ///     from whichever segment currently holds it - it never assumes where a note is, so
    ///     the same call serves both the undo and the redo closure. A plain move carries the
    ///     note's own length along unchanged; a border drag is the same call with a new one.
    ///     A null length is a note with no automation at all, so undoing the drag that
    ///     created one takes it back off the note.
    /// </summary>
    private static void Apply(ProjectTrack track,
        IReadOnlyList<(Note Note, TrackSegment Segment, int Step, double Value, float? End)> targets)
    {
        foreach (var (note, segment, step, value, end) in targets)
        {
            foreach (var s in track.Segments) s.Notes.Remove(note);
            segment.Notes.Add(note);
            note.Step = step;
            note.Value = value;

            if (end is not { } length) note.Automation = null;
            else (note.Automation ??= new AudioKeyframeManager()).End = length;
        }
    }

    private static TrackSegment FindSegment(ProjectTrack track, Note note)
    {
        return track.Segments.First(s => s.Notes.Contains(note));
    }

    private static int GlobalStepOf(ProjectTrack track, Note note)
    {
        var segment = track.Segments.FirstOrDefault(s => s.Notes.Contains(note));
        return segment != null ? track.GlobalStepOf(segment, note) : note.Step;
    }
}

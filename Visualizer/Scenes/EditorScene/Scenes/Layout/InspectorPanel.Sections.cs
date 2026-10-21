using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     The per-subject sections <see cref="Rebuild" /> picks between: a faithful track or slot,
///     a wave track, and the shared-value rows for several notes or placements at once.
/// </summary>
public partial class InspectorPanel
{
    /// <summary>
    ///     The faithful editor's inspector: the selected slot's fields, or the track's own
    ///     summary when nothing is selected. There are no segments here - position is the
    ///     item's index, so the grid rows the piano roll shows would edit nothing.
    /// </summary>
    private void FaithfulSection(FaithfulTrack track)
    {
        // A cut has no value, volume or pan - only what it silences, which Change edits.
        if (_state.SelectedItem is { IsCut: true } cut)
        {
            _form.Header("!cut event");
            _form.InfoRow("Cuts", () => CutTargetDialog.Describe(cut));
            _form.ActionRow("Change", () => OnEditCut?.Invoke(cut));
            return;
        }

        if (_state.SelectedItem?.Note is { } note)
        {
            _form.Header("Sound");
            _form.InfoRow("Instrument", () => note.Instrument.Name);
            _form.ActionRow("Change", () => OnReassignInstrument?.Invoke([note]));
            _form.NumberRow("Value", () => note.Value, v => note.Value = v!.Value,
                -TrackEditorView.MaxValue, TrackEditorView.MaxValue);
            _form.NumberRow("Volume", () => note.Volume, v => note.Volume = v, 0, 500, 5, true);
            _form.NumberRow("Pan", () => note.Pan, v => note.Pan = (float)v!.Value, -100, 100, 10);
            _form.NumberRow("Offset (s)", () => note.Offset, v => note.Offset = v!.Value, -60, 60, 0.05);
            return;
        }

        if (_state.SelectedItem?.Action is { } action)
        {
            _form.Header(action.SoundEvent ?? "Action");
            _form.NumberRow("Value", () => action.Value, v =>
            {
                action.Value = v!.Value;
                action.WorkingValue = action.Value; // loop counters read the working copy
            }, -1e6, 1e6);
            // The event as it will be written out - the only readout that shows a value
            // scale ("@x") or a packed "!pulse"/"!bg" payload for what it is.
            _form.InfoRow("Event", action.Stringify);
            return;
        }

        _form.Header("Faithful track");
        _form.TextRow("Name", () => track.Name, v => _state.RenameTrack(track, v));
        _form.InfoRow("Items", () => track.Items.Count.ToString());
        _form.InfoRow("Length", () => $"{track.DurationMinutes() * 60:0.##} s");
    }

    /// <summary>
    ///     A wave reference track's fields. No tempo and no automation - the file plays at its
    ///     own rate whatever the project does - and no segments, since there is nothing in it
    ///     to edit. "Missing" marks a path that no longer resolves: the clip keeps its saved
    ///     length and draws, it just has nothing to play.
    /// </summary>
    private void WaveSection(WaveTrack track)
    {
        _form.Header("Wave track");
        _form.TextRow("Name", () => track.Name, v => _state.RenameTrack(track, v));
        _form.ColorRow("Color", () => TrackColor?.Invoke(track) ?? default,
            () => OnChangeTrackColor?.Invoke([track]));
        _form.InfoRow("File", () => track.Path.Length == 0
            ? "None"
            : Path.GetFileName(track.Path) + (File.Exists(track.Path) ? "" : " (missing)"));
        _form.ActionRow("Replace", () => OnReplaceWaveFile?.Invoke(track));
        _form.InfoRow("Length", () => $"{track.DurationSeconds:0.##} s");
        // Up to 8x, not the 2x a note gets: a note is played from a Thirty Dollar sound, which
        // is mastered loud (a bleep is about -9 dBFS RMS), while a reference is usually one stem
        // of a finished mix - the MOON "other" stem is -21 dBFS RMS, and several notes sounding
        // together put the song further above it still. Bridging that needs +15 dB or so, and
        // +6 dB was nowhere near it.
        _form.NumberRow("Volume", () => track.Volume, v => track.Volume = v!.Value, 0, 800, 5);
    }

    /// <summary>
    ///     Multi-note selection: independent modifier properties (Value/Volume/Pan/
    ///     Offset/Instrument) are always editable - uniform values show, differing ones
    ///     render empty and committing applies the absolute value to every selected
    ///     note. Automation works the same way, field by field (see
    ///     <see cref="AutomationSection" />).
    /// </summary>
    private void MultiNoteSection(IReadOnlyList<Note> notes)
    {
        var primary = notes[^1]; // last = primary, per EditorState's selection-order convention

        _form.Header($"Note (× {notes.Count})");
        _form.InfoRow("Instrument", () => AllEqual(notes, n => n.Instrument) ? primary.Instrument.Name : "mixed");
        _form.ActionRow("Change", () => OnReassignInstrument?.Invoke(notes));

        // A mixed cut/normal selection edits its non-cut notes only - a cut's value/
        // volume/pan/offset are fixed invariants (see Note.IsCut).
        _form.NumberRow("Value", () => primary.Value,
            v =>
            {
                foreach (var n in notes)
                    if (!n.IsCut)
                        n.Value = v!.Value;
            },
            -TrackEditorView.MaxValue, TrackEditorView.MaxValue,
            mixed: () => !AllEqual(notes, n => n.Value));
        _form.NumberRow("Volume", () => primary.Volume,
            v =>
            {
                foreach (var n in notes)
                    if (!n.IsCut)
                        n.Volume = v;
            },
            0, 500, 5, true, () => !AllEqual(notes, n => n.Volume));
        _form.NumberRow("Pan", () => primary.Pan,
            v =>
            {
                foreach (var n in notes)
                    if (!n.IsCut)
                        n.Pan = (float)v!.Value;
            },
            -100, 100, 10, mixed: () => !AllEqual(notes, n => n.Pan));
        _form.NumberRow("Offset (s)", () => primary.Offset,
            v =>
            {
                foreach (var n in notes)
                    if (!n.IsCut)
                        n.Offset = v!.Value;
            },
            -60, 60, 0.05, mixed: () => !AllEqual(notes, n => n.Offset));

        AutomationSection(notes);
    }

    /// <summary>
    ///     Multi-placement selection: placements own only position, never inspector-
    ///     edited - the Track section (name/tempo/BPM/track automation) shows only when
    ///     every selected placement references the same <see cref="ProjectTrack" />.
    /// </summary>
    private void MultiPlacementSection(IReadOnlyList<TrackPlacement> placements)
    {
        _form.Header($"Clips (× {placements.Count})");
        _form.Header("Track");

        if (!AllEqual(placements, p => p.Track))
        {
            _form.InfoRow("Track", () => "mixed");
            // Color is the one track property a mixed selection can still set: it is per
            // track, but picking one swatch for all of them is exactly what grouping a
            // section of an arrangement by color means.
            ColorRowFor([.. placements.Select(p => p.Track).Distinct()]);
            return;
        }

        var track = placements[0].Track;
        _form.TextRow("Name", () => track.Name, v => _state.RenameTrack(track, v));
        _form.ColorRow("Color", () => TrackColor?.Invoke(track) ?? default,
            () => OnChangeTrackColor?.Invoke([track]));
        _form.CheckRow("Project tempo", () => _state.TrackFollowsRootTiming(track), follows =>
        {
            _state.SetTrackFollowsRootTiming(track, follows);
            Rebuild(); // the own-BPM row appears/disappears
        });
        if (!_state.TrackFollowsRootTiming(track))
            _form.NumberRow("BPM", () => track.Timing.BPM, v => track.Timing.BPM = (float)v!.Value, 1, 9999);

        TrackAutomationSection(track);
    }

    /// <summary>
    ///     A Color row for a group of tracks: the chip shows their shared fill, or nothing
    ///     when they differ - the counterpart of the multi-note rows' "mixed" state - and
    ///     the picked swatch applies to every track in the group.
    /// </summary>
    private void ColorRowFor(IReadOnlyList<ProjectTrack> tracks)
    {
        if (tracks.Count == 0) return;

        _form.ColorRow("Color",
            () => AllEqual(tracks, t => t.ColorIndex) ? TrackColor?.Invoke(tracks[0]) ?? default : default,
            () => OnChangeTrackColor?.Invoke(tracks));
    }

    private static bool AllEqual<TItem, TValue>(IReadOnlyList<TItem> items, Func<TItem, TValue> selector)
    {
        var first = selector(items[0]);
        return items.All(item => Equals(selector(item), first));
    }
}

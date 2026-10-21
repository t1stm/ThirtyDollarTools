using JetBrains.Annotations;
using OpenTK.Windowing.Common;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes;

/// <summary>
///     Every dialog and modal flow the editor opens - file load/save/import/export, the track
///     context menu and its prompts, action values, the sound filter - and the forwarders that
///     let the scene drive whichever modal is on top.
/// </summary>
public partial class EditorInterface
{
    /// <summary>
    ///     Guards <see cref="ShowTrackContextMenu" /> against reopening on every held frame -
    ///     right-press is level-triggered, so a stationary right-click keeps firing.
    /// </summary>
    private DropdownMenu? _trackContextMenuModal;

    /// <summary>Same lazy-fill guard as <see cref="InstrumentEditor.EnsureSounds" />, for the filter picker.</summary>
    private void EnsureSoundFilterItems()
    {
        if (_soundFilterPicker.HasSounds) return;
        _soundFilterPicker.Fill(AllSounds());
    }

    /// <summary>
    ///     Every pickable sound, once - the sample holder's map holds each one
    ///     twice (ID and emoji) when it has an emoji.
    /// </summary>
    private IEnumerable<Sound> AllSounds()
    {
        return _workflow.SampleHolder.StringToSoundReferences.Values.Distinct().OrderBy(sound => sound.Id);
    }

    /// <summary>Public because SoundFilter.snx.csx wires both the Done button and the backdrop dismiss to it.</summary>
    [UsedImplicitly]
    public void CommitAndCloseSoundFilter()
    {
        if (_editingTrackAutomation is { } automation)
            State.Edit(() => automation.Sounds = [.. _soundFilterPicker.Selected]);
        RootPanel.RemoveChild(SoundFilterModal);
        _inspector.Rebuild();
    }

    /// <summary>Wired from TrackEditorPanel.snx.csx - the workflow only exists once the root is drawn.</summary>
    [UsedImplicitly]
    public void OpenInstrumentSelector()
    {
        _instrumentWorkflow.OpenSelector();
    }

    /// <summary>Previews the instrument being edited; false when the instrument editor isn't open.</summary>
    public bool TryPreviewInstrument()
    {
        return _instrumentWorkflow.TryPreview();
    }

    /// <summary>Whether a dialog is open over the editor.</summary>
    public bool HasOpenModal => _dialogHost.HasOpenModal;

    /// <summary>Dismisses the topmost open modal, if any. Used so Escape closes a dialog instead of the editor.</summary>
    public bool TryCloseTopModal()
    {
        return _dialogHost.TryCloseTop();
    }

    /// <summary>Offers a key to the topmost modal's content; see <see cref="DialogHost.TryKeyTop" />.</summary>
    public bool TryKeyTopModal(KeyboardKeyEventArgs e)
    {
        return _dialogHost.TryKeyTop(e);
    }

    /// <summary>Runs the topmost modal's primary action, if it has one. Used so Enter confirms a dialog.</summary>
    public bool TryConfirmTopModal()
    {
        return _dialogHost.TryConfirmTop();
    }

    public void LoadProjectFile(string location)
    {
        _projectIo.Load(location);
    }

    /// <summary>
    ///     Asks first, then falls into the normal <see cref="ImportSequenceFile" /> flow -
    ///     for drops with no extension, where the file being a sequence is a guess.
    /// </summary>
    public void ConfirmImportSequenceFile(string path)
    {
        _dialogHost.Confirm($"\"{Path.GetFileName(path)}\" has no file extension.\n" +
                            $"Import it as a TDW sequence?",
            () => ImportSequenceFile(path),
            confirmLabel: "Continue", confirmClass: "dialog-button-primary");
    }

    /// <summary>
    ///     Shows the faithful/piano-roll/project/cancel choice for a dropped TDW sequence
    ///     file. Import-as-project discards the open project, so it's guarded behind the
    ///     same dirty check as every other destructive action here.
    /// </summary>
    public void ImportSequenceFile(string path)
    {
        var dialog = new ImportDialog(_context, Path.GetFileName(path));
        var modal = _dialogHost.Show(dialog.Element);
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.FaithfulTrackButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            Import(path, ImportMode.Faithful);
        };
        dialog.SingleTrackButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            Import(path, ImportMode.Track);
        };
        dialog.ProjectButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            if (State.Dirty)
                _dialogHost.Confirm(
                    "Importing as a project discards unsaved changes.\n" +
                    "Continue?",
                    () => Import(path, ImportMode.Project),
                    confirmLabel: "Import", confirmClass: "dialog-button-primary");
            else
                Import(path, ImportMode.Project);
        };
    }

    /// <summary>
    ///     Imports a sequence file and, when anything didn't carry over, shows what. A Piano
    ///     Roll track import can be redone as a Faithful Track from there: the track import
    ///     is one undo step, so stepping back over it leaves the project as it was.
    /// </summary>
    private void Import(string path, ImportMode mode)
    {
        if (_projectIo.ImportTdw(path, mode, SoundMap()) is not { IsEmpty: false } warnings) return;

        var dialog = new ImportWarningsDialog(_context, Path.GetFileName(path), mode, warnings, _playfieldLook);
        var modal = _dialogHost.Show(dialog.Element);
        modal.OnDismissRequested = _ => Close();
        dialog.KeepButton.OnClick = _ => Close();
        dialog.FaithfulButton.OnClick = _ =>
        {
            Close();
            State.Undo();
            Import(path, ImportMode.Faithful);
        };
        return;

        void Close()
        {
            _dialogHost.Close(modal);
            dialog.Release();
        }
    }

    /// <summary>
    ///     Every name a sequence may use (ID or emoji) mapped to its sound - the
    ///     importer both filters unknown sounds and canonicalises names through it.
    /// </summary>
    private IReadOnlyDictionary<string, Sound> SoundMap()
    {
        return _workflow.SampleHolder.StringToSoundReferences;
    }

    /// <summary>Back button / Escape: leaves directly when clean, otherwise asks first.</summary>
    public void RequestBack()
    {
        if (!State.Dirty)
        {
            PerformBack();
            return;
        }

        var dialog = new UnsavedChangesDialog(_context, State.Project.Info.Name, State.ProjectPath);
        var modal = _dialogHost.Show(dialog.Element);
        modal.OnConfirmRequested = _ => Save();
        dialog.SaveButton.OnClick = _ => Save();
        dialog.DiscardButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            DiscardChanges();
            PerformBack();
        };
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.CloseButton.OnClick = _ => _dialogHost.Close(modal);
        return;

        void Save()
        {
            _dialogHost.Close(modal);
            _projectIo.Save(PerformBack);
        }
    }

    /// <summary>
    ///     Throws away the unsaved work: the editor scene outlives a trip to the
    ///     home screen, so without this the "discarded" project is still sitting there
    ///     (still dirty) on the way back in. Reverts to the file on disk when there is one,
    ///     otherwise to an empty project.
    /// </summary>
    private void DiscardChanges()
    {
        if (State.ProjectPath is { } path) _projectIo.Load(path);
        else State.NewProject();
    }

    /// <summary>
    ///     "+ Add track" asks which kind first, and the new track's name and color. The new track
    ///     is selected and opened, so the kind that was picked is immediately visible instead of
    ///     being a silent list entry. A wave track asks for its file next and is named after it.
    ///     The dialog is built on first use (the clip palette is styled by then) and kept.
    /// </summary>
    private void ShowTrackTypeDialog()
    {
        var dialog = _trackTypeDialog ??= new TrackTypeDialog(_context, _workflow.AtlasStore,
            _arrangement.ClipPalette, _arrangement.ClipColor);
        dialog.Reset(State.Project.NextTrackName, SoundMap());
        var modal = _dialogHost.Show(dialog.Element, onKey: dialog.HandleKey);

        modal.OnConfirmRequested = _ => Add();
        dialog.AddButton.OnClick = _ => Add();
        dialog.NameInput.OnCommit = _ => Add();
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.CloseButton.OnClick = _ => _dialogHost.Close(modal);
        return;

        void Add()
        {
            _dialogHost.Close(modal);
            var color = dialog.ColorIndex;
            if (dialog.Kind == TrackKind.Wave)
                _dialogHost.ShowFileDialog("Open audio", null, ".wav", path => ImportWaveFile(path, color));
            else
                State.OpenTrack(State.AddTrack(dialog.Kind, dialog.NameInput.Value, color));
        }
    }

    /// <summary>
    ///     Adds a wave reference for a file - picked in the dialog, or dropped on the window.
    ///     The track is created only once the file has decoded (its length is what the clip
    ///     draws) and the decode runs off the update thread, so the model edit is queued back
    ///     onto it rather than made from the task. A file that cannot be read raises its own
    ///     error and adds nothing.
    /// </summary>
    /// <param name="path">The .wav file.</param>
    /// <param name="colorIndex">The new track's clip color, as picked in the Add a track dialog.</param>
    public void ImportWaveFile(string path, int? colorIndex = null)
    {
        Playback.PrepareWave(path).ContinueWith(decode =>
        {
            if (decode.Result is not { } seconds) return;
            _pendingEdits.Enqueue(() => State.AddWaveTrack(path, seconds, colorIndex));
        });
    }

    /// <summary>Replaces the file a wave track plays, keeping the clip where it is.</summary>
    private void ShowWaveReplaceDialog(WaveTrack track)
    {
        _dialogHost.ShowFileDialog("Open audio", null, ".wav", path =>
            Playback.PrepareWave(path).ContinueWith(decode =>
            {
                if (decode.Result is not { } seconds) return;
                _pendingEdits.Enqueue(() => State.SetWaveFile(track, path, seconds));
            }));
    }

    /// <summary>
    ///     Inserts a palette action. One with an amount prompts for it first, exactly as the
    ///     site does - as the whole TDW text, so "!speed@2@x" and the two-value "!pulse"/"!bg"
    ///     payloads need no form of their own.
    /// </summary>
    private void ShowActionValueDialog(FaithfulAction action)
    {
        if (action.Template is null)
        {
            Append(action.Name);
            return;
        }

        var dialog = new ActionValueDialog(_context, action);
        var modal = _dialogHost.Show(dialog.Element);
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.AddButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            Append(dialog.ValueInput.Value);
        };
        return;

        void Append(string tdw)
        {
            if (State.OpenedFaithfulTrack is not { } track) return;
            if (FaithfulItem.Parse(tdw) is { } item) State.AppendItem(track, item);
            else _dialogHost.Alert($"\"{tdw}\" isn't an event this editor can read.");
        }
    }

    /// <summary>
    ///     Right-clicking an action that carries a value reopens its dialog on what the slot
    ///     says now, which is the site's own right-click. The whole TDW text again, so the
    ///     packed "!bg@#ff0000,0.5" and "!pulse@4,100" payloads are editable at all - the
    ///     event's own <c>Stringify</c> writes those two back in their readable form.
    /// </summary>
    private void ShowActionEditDialog(FaithfulItem item)
    {
        if (State.OpenedFaithfulTrack is not { } track || item.Action is not { } action) return;
        if (FaithfulAction.Find(action.SoundEvent) is not { } definition) return;

        var index = track.Items.IndexOf(item);
        if (index < 0) return;

        var dialog = new ActionValueDialog(_context, definition, action.Stringify());
        var modal = _dialogHost.Show(dialog.Element);
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.AddButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            var tdw = dialog.ValueInput.Value;
            if (FaithfulItem.Parse(tdw) is { } edited) State.ReplaceItemAt(track, index, edited);
            else _dialogHost.Alert($"\"{tdw}\" isn't an event this editor can read.");
        };
    }

    /// <summary>
    ///     Right-clicking a cut, or the inspector's Change on one: what it silences. Saving swaps
    ///     the item for the dialog's, one undo step, as the action dialog does.
    /// </summary>
    private void ShowCutTargetDialog(FaithfulItem item)
    {
        if (State.OpenedFaithfulTrack is not { } track) return;

        var index = track.Items.IndexOf(item);
        if (index < 0) return;

        var dialog = new CutTargetDialog(_context, _workflow.AtlasStore, State.Project.Instruments,
            [.. track.TrackSounds()], item, _workflow.SampleHolder.StringToSoundReferences, _playfieldLook);
        var modal = _dialogHost.Show(dialog.Element, onKey: dialog.HandleKey);
        modal.OnDismissRequested = _ => Close();
        modal.OnConfirmRequested = _ => Save();
        dialog.CloseButton.OnClick = _ => Close();
        dialog.CancelButton.OnClick = _ => Close();
        dialog.SaveButton.OnClick = _ => Save();
        return;

        void Close()
        {
            _dialogHost.Close(modal);
            dialog.Release();
        }

        void Save()
        {
            Close();
            State.ReplaceItemAt(track, index, dialog.Result);
        }
    }

    private void ShowTrackContextMenu(ProjectTrack track, float x, float y)
    {
        if (_trackContextMenuModal != null) return;

        var menu = new DropdownMenu(_context, x, y);
        // A wave track has no editor to open and nothing to convert into - it is a file.
        if (track is WaveTrack wave)
        {
            menu.AddItem("Replace file…", () => ShowWaveReplaceDialog(wave));
        }
        else
        {
            menu.AddItem("Open", () => State.OpenTrack(track));
        }

        // The right-press kept an existing multi-selection the track is part of (see
        // TrackListPanel.ContextMenu), so the recolor follows it rather than the one row
        // the pointer happens to be over.
        menu.AddItem("Change color…", () => ShowTrackColorDialog(
            State.SelectedTracks.Contains(track) ? State.SelectedTracks : [track]));
        menu.AddItem("Duplicate…", () => ShowDuplicateTrackDialog(track));
        if (track.Kind != TrackKind.Wave)
            menu.AddItem(track.Kind == TrackKind.Faithful ? "Convert to Piano Roll" : "Convert to Faithful",
                () => ConvertTrack(track));

        menu.AddItem("Remove", () => State.RemoveTrack(track));

        _dialogHost.Root.AddChild(menu);
        _trackContextMenuModal = menu;
        menu.OnDismissRequested = m =>
        {
            _dialogHost.Root.RemoveChild(m);
            _trackContextMenuModal = null;
        };
    }

    /// <summary>
    ///     The recolor swatch grid, reached from a track's context menu and from the
    ///     inspector's Color row. The palette is the arrangement's, so the swatches are
    ///     exactly the fills a clip can take. Several tracks recolor together: the grid
    ///     marks their current swatch only when they already share one.
    /// </summary>
    private void ShowTrackColorDialog(IReadOnlyList<ProjectTrack> tracks)
    {
        if (tracks.Count == 0) return;

        var shared = tracks.All(t => t.ColorIndex == tracks[0].ColorIndex) ? tracks[0].ColorIndex : null;
        var title = tracks.Count == 1 ? tracks[0].Name : $"{tracks.Count} tracks";
        var dialog = new TrackColorDialog(_context, title, _arrangement.ClipPalette,
            _arrangement.ClipColor, shared);
        var modal = _dialogHost.Show(dialog.Element);
        dialog.OnPick = index =>
        {
            _dialogHost.Close(modal);
            State.SetTrackColor(tracks, index);
        };
    }

    /// <summary>Duplicate's name prompt, reached from the track context menu.</summary>
    private void ShowDuplicateTrackDialog(ProjectTrack track)
    {
        var dialog = new TrackContextMenu(_context, $"{track.Name} copy");
        var modal = _dialogHost.Show(dialog.Element);
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.DuplicateButton.OnClick = _ =>
        {
            State.DuplicateTrack(track, dialog.NameInput.Value);
            _dialogHost.Close(modal);
        };
    }

    /// <summary>
    ///     The context menu's kind conversion. A track that can't be converted (nothing in
    ///     it, or a piano roll whose sequence holds no sounds) says so rather than throwing
    ///     out of the menu - the project is untouched either way.
    /// </summary>
    private void ConvertTrack(ProjectTrack track)
    {
        try
        {
            State.ConvertTrack(track);
        }
        catch (Exception e)
        {
            _dialogHost.Alert($"Couldn't convert \"{track.Name}\":\n\n{e.Message}");
        }
    }

    /// <summary>Load/Save, wired from EditorHeader.snx.csx - a .csx only reaches the public surface.</summary>
    [UsedImplicitly]
    public void ShowLoadDialog()
    {
        _dialogHost.ShowFileDialog("Open project", null, ".tdwproj", LoadProjectFile);
    }

    [UsedImplicitly]
    public void SaveProject()
    {
        _projectIo.Save();
    }

    /// <summary>Discards the open project, so it asks first when there is unsaved work.</summary>
    [UsedImplicitly]
    public void NewProject()
    {
        if (!State.Dirty)
        {
            State.NewProject();
            return;
        }

        _dialogHost.Confirm("Starting a new project discards unsaved changes.\nContinue?",
            State.NewProject, confirmLabel: "New project", confirmClass: "dialog-button-primary");
    }

    [UsedImplicitly]
    public void ShowExportDialog()
    {
        var dialog = new ExportDialog(_context);
        dialog.ProjectLabel.SetTextContents(State.Project.Info.Name);
        if (State.Project.Tracks.All(track => track.Kind != TrackKind.Wave))
            dialog.Element.RemoveChild(dialog.WaveNote);

        // Exports land next to the project file; an unsaved project has none and opens home.
        var directory = Path.GetDirectoryName(State.ProjectPath);
        var modal = _dialogHost.Show(dialog.Element);
        modal.OnConfirmRequested = _ => Export();
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.CloseButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.ExportButton.OnClick = _ => Export();
        return;

        void Export()
        {
            _dialogHost.Close(modal);
            if (dialog.Format == ExportFormat.Tdw)
            {
                var style = dialog.Style;
                _dialogHost.ShowFileDialog("Export sequence", $"{State.Project.Info.Name}.tdw", ".tdw",
                    path => _projectIo.ExportTdw(path, style), "Export", directory);
            }
            else
            {
                _dialogHost.ShowFileDialog("Export audio", $"{State.Project.Info.Name}.wav", ".wav",
                    path => Playback.ExportWav(path), "Export", directory);
            }
        }
    }
}

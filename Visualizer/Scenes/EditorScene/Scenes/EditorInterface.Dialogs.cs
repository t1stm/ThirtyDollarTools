using JetBrains.Annotations;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Components;
using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Views;
using VisualizerScene.Settings;

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

    /// <summary>The open project menu, if any - so a second open while it is up does nothing.</summary>
    private DropdownMenu? _projectMenu;

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

    /// <summary>The sound filter's tray stand-in while nothing is picked.</summary>
    private FlexPanel _soundFilterEmpty = null!;

    /// <summary>
    ///     The sound filter's parts that need the picker, which the markup can't reach: the tray
    ///     the picks show in, the count, the filter field and Clear.
    /// </summary>
    private void WireSoundFilter()
    {
        _soundFilterEmpty = EmptyState.Build(_context, Icons.MousePointer2,
            "Nothing picked yet. Click sounds below to add them.", stacked: false);
        SoundFilterInput.Placeholder = "Filter sounds";
        SoundFilterInput.OnValueChanged = input => _soundFilterPicker.Filter(input.Value);
        _soundFilterPicker.OnClearFilter = () => SoundFilterInput.Value = "";
        _soundFilterPicker.OnInstancesChanged = RefreshSoundFilterPicks;
        SoundFilterClear.OnClick = _ => _soundFilterPicker.SetSelected([]);
        KeyChip.Add(SoundFilterDone, "Enter");
        RefreshSoundFilterPicks();
    }

    private void RefreshSoundFilterPicks()
    {
        var picked = _soundFilterPicker.Instances.Count;
        SoundFilterCount.SetTextContents($"{picked} picked");
        UIElement shown = picked > 0 ? _soundFilterPicker.Tray : _soundFilterEmpty;
        UIElement hidden = picked > 0 ? _soundFilterEmpty : _soundFilterPicker.Tray;
        if (hidden.Parent == SoundFilterTray) SoundFilterTray.RemoveChild(hidden);
        if (shown.Parent != SoundFilterTray) SoundFilterTray.AddChild(shown);
    }

    /// <summary>Public because SoundFilter.snx.csx wires every way out of the sound filter to it.</summary>
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

    /// <summary>A dropped project file. Asks first when the open project has unsaved changes.</summary>
    public void LoadProjectFile(string location)
    {
        AskToSave(OpenTitle, Load, Load);
        return;

        void Load()
        {
            _projectIo.Load(location);
        }
    }

    /// <summary>
    ///     Asks first, then falls into the normal <see cref="ImportSequenceFile" /> flow -
    ///     for drops with no extension, where the file being a sequence is a guess.
    /// </summary>
    public void ConfirmImportSequenceFile(string path)
    {
        _dialogHost.Confirm("Import it as a TDW sequence?", $"“{Path.GetFileName(path)}” has no file extension.",
            () => ImportSequenceFile(path),
            confirmLabel: "Continue", confirmClass: "dialog-button-primary");
    }

    /// <summary>
    ///     Reads a dropped TDW sequence file and asks how to import it: as a faithful track, a
    ///     piano-roll track or a new project, each with what it keeps and loses. A file that
    ///     doesn't parse is reported and opens nothing. Replacing the project needs no second
    ///     confirm: the dialog says so on that choice, unsaved changes included.
    /// </summary>
    public void ImportSequenceFile(string path)
    {
        var sounds = SoundMap();
        if (_projectIo.ReadTdw(path, sounds) is not ({ } sequence, { } preview)) return;

        var dialog = new ImportDialog(_context, _workflow.AtlasStore, Path.GetFileName(path), sequence, preview,
            sounds, _arrangement.ClipPalette, State.Project.Info.Name, State.Dirty);
        var modal = _dialogHost.Show(dialog.Element, onKey: dialog.HandleKey);

        modal.OnDismissRequested = _ => Close();
        modal.OnConfirmRequested = _ => Confirm();
        dialog.ImportButton.OnClick = _ => Confirm();
        dialog.NameInput.OnCommit = _ => Confirm();
        dialog.CancelButton.OnClick = _ => Close();
        return;

        void Close()
        {
            _dialogHost.Close(modal);
            dialog.Release();
        }

        void Confirm()
        {
            // Enter and the name field's commit land here too; a way that can't import does nothing.
            if (!dialog.CanImport) return;
            Close();
            var name = dialog.NameInput.Value.Trim();
            Import(path, sequence, dialog.Mode, name.Length > 0 ? name : Path.GetFileNameWithoutExtension(path));
        }
    }

    /// <summary>
    ///     Imports a sequence file and, when anything didn't carry over, shows what. A Piano
    ///     Roll track import can be redone as a Faithful Track from there: the track import
    ///     is one undo step, so stepping back over it leaves the project as it was.
    /// </summary>
    private void Import(string path, Sequence sequence, ImportMode mode, string name)
    {
        if (_projectIo.ImportTdw(path, sequence, mode, name, SoundMap()) is not { IsEmpty: false } warnings) return;

        var dialog = new ImportWarningsDialog(_context, Path.GetFileName(path), mode, warnings, _playfieldLook);
        var modal = _dialogHost.Show(dialog.Element);
        modal.OnDismissRequested = _ => Close();
        modal.OnConfirmRequested = _ => Close();
        dialog.KeepButton.OnClick = _ => Close();
        dialog.FaithfulButton.OnClick = _ =>
        {
            Close();
            State.Undo();
            Import(path, sequence, ImportMode.Faithful, name);
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
        AskToSave("Save before leaving?", PerformBack, () =>
        {
            DiscardChanges();
            PerformBack();
        });
    }

    private const string OpenTitle = "Save before opening another project?";

    /// <summary>
    ///     Runs <paramref name="proceed" /> straight away when nothing is unsaved. Otherwise asks:
    ///     Save saves and then proceeds (not when the save failed or its dialog was cancelled),
    ///     Discard runs <paramref name="discard" />, Cancel leaves everything as it was.
    /// </summary>
    private void AskToSave(string title, Action proceed, Action discard)
    {
        if (!State.Dirty)
        {
            proceed();
            return;
        }

        var dialog = new UnsavedChangesDialog(_context, State.Project.Info.Name, State.ProjectPath, title);
        var modal = _dialogHost.Show(dialog.Element);
        modal.OnConfirmRequested = _ => Save();
        dialog.SaveButton.OnClick = _ => Save();
        dialog.DiscardButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            discard();
        };
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        return;

        void Save()
        {
            _dialogHost.Close(modal);
            _projectIo.Save(proceed);
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
    ///     Inserts a palette action. One with an amount opens its form first, as the site
    ///     opens its popup; the rest go straight in.
    /// </summary>
    private void ShowActionValueDialog(FaithfulAction action)
    {
        if (State.OpenedFaithfulTrack is not { } track) return;
        if (action.Template is null)
        {
            if (FaithfulItem.Parse(action.Name) is { } item) State.AppendItem(track, item);
            return;
        }

        ShowActionForm(track, action, track.Items.Count, null);
    }

    /// <summary>
    ///     Right-clicking an action that carries a value reopens its form on what the slot says
    ///     now, which is the site's own right-click. Saving swaps the item, one undo step.
    /// </summary>
    private void ShowActionEditDialog(FaithfulItem item)
    {
        if (State.OpenedFaithfulTrack is not { } track || item.Action is not { } action) return;
        if (FaithfulAction.Find(action.SoundEvent) is not { } definition) return;

        var index = track.Items.IndexOf(item);
        if (index >= 0) ShowActionForm(track, definition, index, action);
    }

    /// <summary>The action form for a new item (<paramref name="current" /> null, added at the end) or an edit.</summary>
    private void ShowActionForm(FaithfulTrack track, FaithfulAction action, int index, BaseEvent? current)
    {
        var dialog = new ActionValueDialog(_context, action, track, index, current, _playfieldLook);
        var modal = _dialogHost.Show(dialog.Element, onKey: dialog.HandleKey);
        modal.OnDismissRequested = _ => Close();
        modal.OnConfirmRequested = _ => Save();
        dialog.CancelButton.OnClick = _ => Close();
        dialog.SaveButton.OnClick = _ => Save();
        dialog.Focus();
        return;

        void Close()
        {
            _dialogHost.Close(modal);
            dialog.Release();
        }

        // A field that holds nothing the slot can be (an empty number, a short color) keeps the
        // form open; the card already says what is wrong.
        void Save()
        {
            if (dialog.Result is not { } result) return;
            Close();
            if (current is null) State.AppendItem(track, result);
            else State.ReplaceItemAt(track, index, result);
        }
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

    /// <summary>
    ///     A track row's right-click menu. Right-clicking a row outside the selection selects it
    ///     first, so the inspector, the highlight and the menu all name the same track; a row
    ///     inside a multi-selection keeps it, and the recolor follows the whole selection. The
    ///     menu hangs beside the row rather than over it, and C, D and R run its rows while it is up.
    /// </summary>
    private void ShowTrackContextMenu(ProjectTrack track, float x, float y)
    {
        if (_trackContextMenuModal != null) return;
        if (!State.SelectedTracks.Contains(track)) State.SelectTrack(track);

        var menu = _trackList.RowOf(track) is { } row ? DropdownMenu.Beside(row) : new DropdownMenu(_context, x, y);
        // The menu's own box is the ring; the rows go in a second box inside it.
        var box = new Dropdown(_context) { Classes = ["context-menu"] };
        var keys = new Dictionary<Keys, DropdownItem>();
        menu.Menu.SetClass("context-menu-ring", true);
        menu.Menu.AddChild(box);
        box.AddChild(new FlexPanel(_context)
        {
            Classes = ["menu-heading"],
            Children = [new ClippedLabel(_context, track.Name) { Classes = ["menu-heading-label"] }]
        });

        // A wave track has no editor to open and nothing to convert into - it is a file.
        if (track is WaveTrack wave) Item("Replace file…", () => ShowWaveReplaceDialog(wave));
        else Item("Open", () => State.OpenTrack(track));

        Rule();
        Item("Change color…", () => ShowTrackColorDialog(State.SelectedTracks), Keys.C);
        Item("Duplicate…", () => ShowDuplicateTrackDialog(track), Keys.D);
        if (track.Kind != TrackKind.Wave)
            Item(track.Kind == TrackKind.Faithful ? "Convert to Piano Roll" : "Convert to Faithful",
                () => ConvertTrack(track));

        Rule();
        Item("Remove", () => State.RemoveTrack(track), Keys.R).Label.SetClass("danger-label", true);

        // The menu is modal, so its letters reach nothing behind it.
        menu.OnKeyRequested = e =>
        {
            if (e.Control || e.Alt || e.Command || !keys.TryGetValue(e.Key, out var item)) return false;
            item.OnClick!(item);
            return true;
        };

        _dialogHost.Root.AddChild(menu);
        _trackContextMenuModal = menu;
        menu.OnDismissRequested = m =>
        {
            _dialogHost.Root.RemoveChild(m);
            _trackContextMenuModal = null;
        };
        return;

        // A row with a key leads with its chip; one without keeps the chip's room, so every
        // label starts at the same x.
        DropdownItem Item(string text, Action action, Keys? key = null)
        {
            var item = new DropdownItem(_context, text)
            {
                Classes = ["context-menu-item"],
                OnClick = _ =>
                {
                    menu.Close();
                    action();
                }
            };
            if (key is { } k)
            {
                KeyChip.Add(item, k.ToString()).Children[0].SetClass("context-menu-key-chip", true);
                keys[k] = item;
            }
            else
            {
                item.Children = [new Panel(_context) { Classes = ["context-menu-key-gap"] }, item.Label];
            }

            item.SetClass("context-menu-key-item", true);
            box.AddChild(item);
            return item;
        }

        void Rule()
        {
            box.AddChild(new FlexPanel(_context)
            {
                Classes = ["context-menu-rule-gap"],
                Children = [new Panel(_context) { Classes = ["context-menu-rule"] }]
            });
        }
    }

    /// <summary>
    ///     The recolor swatch grid, reached from a track's context menu and from the
    ///     inspector's Color row. The palette is the arrangement's, so the swatches are
    ///     exactly the fills a clip can take. Several tracks recolor together: the grid
    ///     rings their current swatch only when they already share one, and counts a mix.
    /// </summary>
    private void ShowTrackColorDialog(IReadOnlyList<ProjectTrack> tracks)
    {
        if (tracks.Count == 0) return;

        var subject = tracks.Count == 1 ? tracks[0].Name : $"{tracks.Count} tracks";
        var dialog = new TrackColorDialog(_context, subject, _arrangement.ClipPalette,
            _arrangement.ClipColor, [.. tracks.Select(t => t.ColorIndex)]);
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
        var dialog = new DuplicateTrackDialog(_context, track.Name,
            ThirtyDollarProject.CopyName(track.Name, State.Project.Tracks.Select(t => t.Name)));
        var modal = _dialogHost.Show(dialog.Element);
        modal.OnConfirmRequested = _ => Duplicate();
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.DuplicateButton.OnClick = _ => Duplicate();
        dialog.Focus();
        return;

        void Duplicate()
        {
            _dialogHost.Close(modal);
            State.DuplicateTrack(track, dialog.Name);
        }
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
            _dialogHost.Alert($"Couldn't convert “{track.Name}”", e);
        }
    }

    /// <summary>
    ///     Open/Save, wired from EditorHeader.snx.csx - a .csx only reaches the public surface.
    ///     Open asks about unsaved changes before the picker, the way leaving the editor does;
    ///     the changes are only gone once a file is actually picked.
    /// </summary>
    [UsedImplicitly]
    public void ShowLoadDialog()
    {
        AskToSave(OpenTitle, Pick, Pick);
        return;

        void Pick()
        {
            _dialogHost.ShowFileDialog("Open project", null, ".tdwproj", _projectIo.Load);
        }
    }

    [UsedImplicitly]
    public void SaveProject()
    {
        _projectIo.Save();
    }

    public void SaveProjectAs()
    {
        _projectIo.SaveAs();
    }

    /// <summary>"Add track" and its key: asks which kind, see <see cref="ShowTrackTypeDialog" />.</summary>
    public void AddTrack()
    {
        ShowTrackTypeDialog();
    }

    /// <summary>
    ///     The menu under the project's name in the top bar: every file action, each with its key.
    ///     Export is only here - the bar keeps Save, and New and Open while the project is new.
    /// </summary>
    [UsedImplicitly]
    public void ShowProjectMenu(Button anchor)
    {
        if (_projectMenu != null) return;

        var menu = DropdownMenu.Below(anchor);
        menu.ID = "project-menu";
        Item("New project", Bind.EditorNew, NewProject);
        Item("Open…", Bind.EditorOpen, ShowLoadDialog);
        menu.Menu.AddChild(new Panel(_context) { Classes = ["menu-rule"] });
        KeyChip.SetLit(Item("Save", Bind.EditorSave, SaveProject), State.Dirty);
        Item("Save As…", Bind.EditorSaveAs, SaveProjectAs);
        Item("Export…", Bind.EditorExport, ShowExportDialog);

        // The name keeps its hovered fill while its menu hangs from it.
        anchor.SetClass("header-menu-button-open", true);
        menu.OnDismissRequested = m =>
        {
            _dialogHost.Root.RemoveChild(m);
            anchor.SetClass("header-menu-button-open", false);
            _projectMenu = null;
        };
        _dialogHost.Root.AddChild(menu);
        _projectMenu = menu;
        return;

        DropdownItem Item(string text, Bind bind, Action action)
        {
            var item = KeyChip.Add(menu.AddItem(text, action), Keybinds.Get(bind).ToString());
            item.SetClass("menu-key-item", true);
            item.Children[0].SetClass("key-chip-menu", true);
            return item;
        }
    }

    /// <summary>Discards the open project, so it asks first when there is unsaved work.</summary>
    [UsedImplicitly]
    public void NewProject()
    {
        AskToSave("Save before starting a new project?", State.NewProject, State.NewProject);
    }

    [UsedImplicitly]
    public void ShowExportDialog()
    {
        var dialog = new ExportDialog(_context);
        dialog.ProjectLabel.SetTextContents(State.Project.Info.Name);
        if (State.Project.Tracks.All(track => track.Kind != TrackKind.Wave))
            dialog.Element.RemoveChild(dialog.WaveNote);
        dialog.SetTracks(State.Project.Tracks.Count(track => track.Kind != TrackKind.Wave));

        // Exports land next to the project file; an unsaved project has none and opens home.
        var directory = Path.GetDirectoryName(State.ProjectPath);
        var modal = _dialogHost.Show(dialog.Element, onKey: dialog.HandleKey);

        // Measured off the update thread; the answer comes back onto it like any other edit.
        Playback.ExportLengthMs().ContinueWith(length =>
        {
            var ms = length.IsCompletedSuccessfully ? length.Result : (long?)null;
            _pendingEdits.Enqueue(() => dialog.SetLength(ms));
        });
        modal.OnConfirmRequested = _ => Export();
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
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

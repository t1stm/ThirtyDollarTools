using System.Diagnostics;
using EditorScene.Scenes.Components;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Bars;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.File_Selector;
using Sundex.Components.Scroll;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes;

public class EditorInterface
{
    private const float HeaderHeight = 32;
    private const float TrackColumnWidth = 260;
    private const long BackupIntervalMs = 5 * 60_000;
    private static readonly string BackupDirectory = Path.Combine(AppContext.BaseDirectory, "Editor Backups");

    // Subtle-filled look for code-built buttons (the "+ Add track" row and the track
    // column's transport controls). Code-built children never receive the stylesheet
    // (ApplyStyleSheet runs on the XML tree only), so the menu-button fill/hover is set
    // inline here rather than via the .ss class.
    private static readonly Vector4 MenuFillColor = new(0.2f, 0.204f, 0.29f, 1f); // #33344a
    private static readonly Vector4 MenuFillHoverColor = new(0.247f, 0.255f, 0.376f, 1f); // #3f4160
    private static readonly Vector4 TimeColor = new(0.337f, 0.373f, 0.537f, 1f); // #565f89
    private static readonly Vector4 ProgressBackColor = new(0.251f, 0.251f, 0.376f, 1f); // #404060
    private static readonly Vector4 ProgressForeColor = new(0.478f, 0.635f, 0.968f, 1f); // #7aa2f7

    private readonly ArrangementView _arrangement;
    private readonly Button _addTrackRow;
    private readonly UIContext _context;
    private readonly FlexPanel _gridArea;
    private readonly InspectorPanel _inspector;
    private readonly Panel _inspectorColumn;
    private readonly LaneHeader _laneHeader;
    private readonly TextInput _openedTrackName;
    private readonly Button _playButton;
    private readonly Label _projectBpm;
    private readonly Label _projectName;
    private readonly Button _instrumentButton;
    private readonly InstrumentSelector _instrumentSelector;
    private readonly ModalLayer _instrumentSelectorModal;
    private readonly InstrumentEditor _instrumentEditor;
    private readonly ModalLayer _instrumentEditorModal;
    private Instrument? _editingInstrument;
    private Note? _reassignTarget;
    private readonly ModalLayer _soundFilterModal;
    private readonly SoundPicker _soundFilterPicker;
    private TrackAutomation? _editingTrackAutomation;
    private readonly Panel _trackColumn;
    private readonly TrackEditorView _trackEditor;
    private readonly FlexPanel _trackEditorPanel;
    private readonly ScrollView _trackList;
    private readonly ProgressBar _transportProgress;
    private readonly Label _elapsedLabel;
    private readonly Label _totalLabel;
    private readonly ThirtyDollarWorkflow _workflow;

    private readonly string _defaultTitle;
    private readonly Stopwatch _sinceBackup = Stopwatch.StartNew();

    private bool _editorOpen;

    /// <summary>Scenes are constructed at startup; only the active editor owns the window title.</summary>
    private bool _titleActive;

    public EditorInterface(UIContext context, ThirtyDollarWorkflow workflow, Action back)
    {
        _context = context;
        _workflow = workflow;
        OnBack = back;

        var sundexContext = new SundexContext(context);
        var componentSource = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = "Scenes/Layout/EditorInterface.snx.xml" }
        });

        Component = sundexContext.NewComponent(componentSource.Value);
        RootPanel = Component.Element as Panel ?? throw new Exception("Root panel not found");

        RootPanel.DrawTo(context);

        var ids = Component.RegisteredIDs;
        _projectName = (Label)ids["project-name"];
        _projectBpm = (Label)ids["project-bpm"];
        _trackColumn = (Panel)ids["track-column"];
        _gridArea = (FlexPanel)ids["grid-area"];
        _inspectorColumn = (Panel)ids["inspector-column"];

        Playback = new EditorPlayback(workflow, State);

        _defaultTitle = workflow.Game.Title;
        ((Button)ids["load-button"]).OnClick = _ => ShowFileDialog(null, ".tdwproj", LoadProjectFile);
        ((Button)ids["save-button"]).OnClick = _ => SaveProject();
        ((Button)ids["export-button"]).OnClick = _ => ShowExportDialog();

        // The column body stacks the scrollable track list above the transport
        // controls: the list is percent-height, so it yields whatever room the
        // auto-sized transport section (below) doesn't need — no separate full-width
        // bottom bar, no magic footer-height constant to keep in sync.
        var trackColumnBody = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };
        _trackColumn.AddChild(trackColumnBody);

        _trackList = new ScrollView(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Spacing = 4
        };
        trackColumnBody.AddChild(_trackList);

        // "Add track" lives at the end of the track list: a subtle-filled button
        // matching the menu bar. Hover swaps only the background RGB (per the
        // PropagateAlpha rule); code-built children get no stylesheet state[].
        var addTrackFill = new ColoredPlane { Color = MenuFillColor };
        _addTrackRow = new Button(context, "+ Add track")
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 36,
            FontSizePx = 14f,
            BorderRadius = 6,
            Background = addTrackFill,
            OnClick = _ => State.AddTrack(),
            OnHoverEnter = _ => addTrackFill.Color = MenuFillHoverColor,
            OnHoverExit = _ => addTrackFill.Color = MenuFillColor
        };
        _trackList.AddChild(_addTrackRow);

        _elapsedLabel = new Label(context, "0:00") { FontSizePx = 12f, Color = TimeColor };
        _totalLabel = new Label(context, "0:00") { FontSizePx = 12f, Color = TimeColor };
        _transportProgress = new ProgressBar(context,
            new ColoredPlane { Color = ProgressBackColor }, new ColoredPlane { Color = ProgressForeColor })
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 8,
            BorderRadius = 4
        };
        var progressRow = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Spacing = 8,
            VerticalAlign = Align.Center,
            Children = [_elapsedLabel, _transportProgress, _totalLabel]
        };

        _playButton = TransportButton(context, "Play", Playback.PlayPause);
        _playButton.Width = LiteralOrComputable.Percent(50);
        var stopButton = TransportButton(context, "Stop", Playback.Stop);
        stopButton.Width = LiteralOrComputable.Percent(50);
        var transportButtonsRow = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Spacing = 8,
            Children = [_playButton, stopButton]
        };

        var backButton = TransportButton(context, "Back", RequestBack);
        backButton.Width = LiteralOrComputable.Percent(100);

        var transportSection = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Padding = 8,
            Spacing = 8,
            Children = [Divider(context), progressRow, transportButtonsRow, Divider(context), backButton]
        };
        trackColumnBody.AddChild(transportSection);

        _arrangement = new ArrangementView(context, State)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };
        _laneHeader = new LaneHeader(context, State, _arrangement)
        {
            Width = LaneHeader.GutterWidth,
            Height = LiteralOrComputable.Percent(100)
        };
        _gridArea.AddChild(_laneHeader);
        _gridArea.AddChild(_arrangement);
        _arrangement.OnOpenTrack = State.OpenTrack;
        _arrangement.OnSeekQuarters = Playback.Seek;

        _trackEditor = new TrackEditorView(context, State)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };
        _openedTrackName = new TextInput(context, "")
        {
            FontSizePx = 15f,
            Width = 220,
            BorderRadius = 4,
            Background = new ColoredPlane { Color = new Vector4(0.15f, 0.16f, 0.21f, 1f) },
            OnValueChanged = input =>
            {
                if (State.OpenedTrack is { } track) State.RenameTrack(track, input.Value);
            }
        };

        // The instrument selector/editor open as modals (add/remove on the root, the
        // tested show-hide pattern) instead of a DropDownLabel — hidden-panel toggling
        // doesn't manage the render queue.
        _instrumentSelector = new InstrumentSelector(context);
        _instrumentSelectorModal = new ModalLayer(context);
        _instrumentSelectorModal.AddChild(_instrumentSelector);
        _instrumentSelectorModal.OnDismissRequested = modal =>
        {
            RootPanel.RemoveChild(modal);
            _reassignTarget = null;
        };
        _instrumentSelector.OnPick = instrument =>
        {
            ApplyInstrumentPick(instrument);
            RootPanel.RemoveChild(_instrumentSelectorModal);
        };
        _instrumentSelector.OnNew = () =>
        {
            _editingInstrument = null;
            _instrumentEditor!.Load("Instrument", []);
            RootPanel.RemoveChild(_instrumentSelectorModal);
            OpenInstrumentEditor();
        };
        _instrumentSelector.OnEdit = instrument =>
        {
            _editingInstrument = instrument;
            _instrumentEditor!.Load(instrument.Name, instrument.Sounds, instrument.Adjustments);
            RootPanel.RemoveChild(_instrumentSelectorModal);
            OpenInstrumentEditor();
        };
        _instrumentSelector.OnDelete = instrument =>
        {
            // Both are ModalLayers pinned to the same top z-index, so they'd collide -
            // close the selector while the confirm dialog is up, reopening it after.
            RootPanel.RemoveChild(_instrumentSelectorModal);

            var dialog = new ConfirmDialog(context, $"Delete \"{instrument.Name}\"?\n" +
                                                     "This removes it from every note that uses it.");
            var modal = ShowModal(dialog);
            dialog.CancelButton.OnClick = _ =>
            {
                RootPanel.RemoveChild(modal);
                RootPanel.AddChild(_instrumentSelectorModal);
            };
            dialog.ConfirmButton.OnClick = _ =>
            {
                State.DeleteInstrumentEverywhere(instrument);
                RootPanel.RemoveChild(modal);
                _instrumentSelector.Fill(State.Project.Instruments);
                RootPanel.AddChild(_instrumentSelectorModal);
            };
        };

        _instrumentEditor = new InstrumentEditor(context, workflow.AtlasStore);
        _instrumentEditorModal = new ModalLayer(context);
        _instrumentEditorModal.AddChild(_instrumentEditor);
        _instrumentEditorModal.OnDismissRequested = modal => RootPanel.RemoveChild(modal);
        _instrumentEditor.DoneButton.OnClick = _ => CommitInstrumentEditor();
        _instrumentEditor.SoundsPicker.OnPreviewSound = Playback.PreviewSound;
        _instrumentEditor.PreviewButton.OnClick = _ =>
            Playback.PreviewInstrument(_instrumentEditor.SoundsPicker.Selected
                .Select(sound => (sound, _instrumentEditor.SoundsPicker.Adjustments.GetValueOrDefault(sound)
                                         ?? new SoundAdjustment())));

        _instrumentButton = new Button(context, "Instrument: —")
        {
            OnClick = _ =>
            {
                _reassignTarget = null;
                OpenInstrumentSelector();
            }
        };

        // A second, independent sound picker in multi-select mode: the track-automation
        // sound filter. Mirrors _soundPicker/_soundModal above but commits a whole set
        // via a "Done" button instead of picking one sound and closing immediately.
        _soundFilterPicker = new SoundPicker(context, workflow.AtlasStore) { Width = 640, MultiSelect = true };
        var soundFilterList = new ScrollView(context) { Width = 640, Height = 440 };
        soundFilterList.AddChild(_soundFilterPicker);
        var doneButton = new Button(context, "Done");
        var soundFilterFrame = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = 640,
            Padding = 10,
            Spacing = 8,
            Background = new ColoredPlane { Color = new Vector4(0.086f, 0.086f, 0.118f, 1f) },
            Children = [soundFilterList, doneButton]
        };
        _soundFilterModal = new ModalLayer(context);
        _soundFilterModal.AddChild(soundFilterFrame);
        doneButton.OnClick = _ => CommitAndCloseSoundFilter();
        // Dismissing via the backdrop commits too — clicking outside shouldn't discard picks.
        _soundFilterModal.OnDismissRequested = _ => CommitAndCloseSoundFilter();

        var backToArrangement = new Button(context, "← Arrangement") { OnClick = _ => State.CloseTrack() };
        var addSegment = new Button(context, "+ Segment")
        {
            OnClick = _ =>
            {
                if (State.OpenedTrack is { } track) State.SelectSegment(State.AddSegment(track));
            }
        };
        var removeSegment = new Button(context, "− Segment")
        {
            OnClick = _ =>
            {
                // RemoveSegment refuses on the last segment (library invariant) — just a no-op here.
                if (State is { OpenedTrack: { } track, SelectedSegment: { } segment })
                    State.RemoveSegment(track, segment);
            }
        };
        // Percent-width spacer soaks up the free space so the segment buttons land flush
        // against the bar's right edge — this framework has no space-between align.
        var editorBarSpacer = new Panel(context) { Width = LiteralOrComputable.Percent(100) };
        var editorBar = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 40,
            Spacing = 12,
            Padding = 6,
            Children =
            [
                backToArrangement, _openedTrackName, _instrumentButton, editorBarSpacer, addSegment, removeSegment
            ]
        };
        _trackEditorPanel = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Children = [editorBar, _trackEditor]
        };

        _inspector = new InspectorPanel(context, State)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };
        _inspectorColumn.AddChild(_inspector);
        _inspector.OnEditTrackAutomationSounds = automation =>
        {
            EnsureSoundFilterItems();
            _editingTrackAutomation = automation;
            _soundFilterPicker.SetSelected(automation.Sounds ?? []);
            RootPanel.AddChild(_soundFilterModal);
        };
        _inspector.OnReassignInstrument = note =>
        {
            _reassignTarget = note;
            OpenInstrumentSelector();
        };

        State.OnProjectChanged = () =>
        {
            RefreshProject();
            Playback.NotifyModelChanged();
        };
        State.OnInstrumentsChanged = RefreshActiveInstrument;
        State.OnSelectionChanged = _ =>
        {
            RefreshSelection();
            _inspector.Rebuild();
        };
        State.OnPlacementSelectionChanged = _ => _arrangement.RefreshSelection();
        State.OnChannelsChanged = () =>
        {
            _laneHeader.RefreshChannels();
            Playback.NotifyChannelsChanged();
        };
        State.OnOpenedTrackChanged = track =>
        {
            SwapGridView(track);
            _inspector.Rebuild();
        };
        State.OnNoteSelectionChanged = _ =>
        {
            _trackEditor.InvalidateLayout();
            _inspector.Rebuild();
        };
        State.OnSegmentSelectionChanged = _ =>
        {
            _trackEditor.InvalidateLayout();
            _inspector.Rebuild();
        };
        _trackEditor.OnPreviewNote = Playback.PreviewNote;
        _trackEditor.OnSeekQuarters = Playback.Seek;
        RefreshProject();
    }

    /// <summary>Subtle-filled button matching the menu bar/"+ Add track" look — code-built
    /// children get no stylesheet, so the fill/hover swap is wired here instead of via the
    /// .ss <c>menu-button</c> class.</summary>
    private static Button TransportButton(UIContext context, string label, Action onClick)
    {
        var fill = new ColoredPlane { Color = MenuFillColor };
        return new Button(context, label, fill)
        {
            FontSizePx = 13f,
            BorderRadius = 6,
            OnClick = _ => onClick(),
            OnHoverEnter = _ => fill.Color = MenuFillHoverColor,
            OnHoverExit = _ => fill.Color = MenuFillColor
        };
    }

    /// <summary>A 1px full-width rule, same color as the header/menu dividers.</summary>
    private static Panel Divider(UIContext context)
    {
        return new Panel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 1,
            Background = new ColoredPlane { Color = MenuFillColor }
        };
    }

    /// <summary>
    ///     Shift: the note editor snaps values to 0.2 instead of 1, and scrolling a sound
    ///     row in the instrument editor adjusts pan instead of value.
    ///     Ctrl: the arrangement wheel zooms instead of panning, and scrolling a sound row
    ///     in the instrument editor adjusts volume instead of value.
    /// </summary>
    public void SetModifiers(bool shift, bool ctrl)
    {
        _trackEditor.FineSnap = shift;
        _trackEditor.WheelZooms = ctrl;
        _arrangement.WheelZooms = ctrl;
        _instrumentEditor.SoundsPicker.ShiftHeld = shift;
        _instrumentEditor.SoundsPicker.CtrlHeld = ctrl;
    }

    private void SwapGridView(ProjectTrack? track)
    {
        if (track != null) _openedTrackName.Value = track.Name;
        if ((track != null) == _editorOpen) return;
        _editorOpen = track != null;

        if (_editorOpen)
        {
            _gridArea.RemoveChild(_laneHeader);
            _gridArea.RemoveChild(_arrangement);
            _gridArea.AddChild(_trackEditorPanel);
            _trackEditor.CenterOnZero();
            RefreshActiveInstrument();
        }
        else
        {
            _gridArea.RemoveChild(_trackEditorPanel);
            _gridArea.AddChild(_laneHeader);
            _gridArea.AddChild(_arrangement);
            _arrangement.Refresh();
        }
    }

    /// <summary>Same lazy-fill guard as <see cref="InstrumentEditor.EnsureSounds" />, for the filter picker.</summary>
    private void EnsureSoundFilterItems()
    {
        if (_soundFilterPicker.HasSounds) return;
        _soundFilterPicker.Fill(_workflow.SampleHolder.StringToSoundReferences.Keys.Order());
    }

    private void CommitAndCloseSoundFilter()
    {
        if (_editingTrackAutomation is { } automation)
            State.Edit(() => automation.Sounds = _soundFilterPicker.Selected.ToList());
        RootPanel.RemoveChild(_soundFilterModal);
        _inspector.Rebuild();
    }

    private void OpenInstrumentSelector()
    {
        _instrumentSelector.Fill(State.Project.Instruments);
        RootPanel.AddChild(_instrumentSelectorModal);
    }

    private void OpenInstrumentEditor()
    {
        _instrumentEditor.EnsureSounds(_workflow.SampleHolder.StringToSoundReferences.Keys.Order());
        RootPanel.AddChild(_instrumentEditorModal);
    }

    private void CommitInstrumentEditor()
    {
        var name = string.IsNullOrWhiteSpace(_instrumentEditor.NameInput.Value)
            ? "Instrument"
            : _instrumentEditor.NameInput.Value;

        if (_editingInstrument is { } existing)
        {
            State.RenameInstrument(existing, name);
            State.SetInstrumentSounds(existing, _instrumentEditor.SoundsPicker.Selected, _instrumentEditor.SoundsPicker.Adjustments);
        }
        else
        {
            var created = State.AddInstrument(name);
            State.SetInstrumentSounds(created, _instrumentEditor.SoundsPicker.Selected, _instrumentEditor.SoundsPicker.Adjustments);
            ApplyInstrumentPick(created);
        }

        RootPanel.RemoveChild(_instrumentEditorModal);
        RefreshActiveInstrument();
    }

    /// <summary>
    ///     "Picking" an instrument means setting it active, unless the selector was
    ///     opened from the inspector's "Change" action targeting one note — then it
    ///     reassigns that note instead.
    /// </summary>
    private void ApplyInstrumentPick(Instrument instrument)
    {
        if (_reassignTarget is { } note) State.Edit(() => note.Instrument = instrument);
        else State.ActiveInstrument = instrument;
        _reassignTarget = null;
        RefreshActiveInstrument();
    }

    private void RefreshActiveInstrument()
    {
        _instrumentButton.Label.SetTextContents($"Instrument: {State.ActiveInstrument?.Name ?? "—"}");
        _instrumentButton.InvalidateLayout();
    }

    public EditorState State { get; } = new();
    public EditorPlayback Playback { get; }

    public Action OnBack { get; }
    [UsedImplicitly] public SundexComponent Component { get; }
    public Panel RootPanel { get; }

    /// <summary>Dismisses the topmost open modal, if any. Used so Escape closes a dialog instead of the editor.</summary>
    public bool TryCloseTopModal()
    {
        var modal = RootPanel.Children.OfType<ModalLayer>().LastOrDefault();
        if (modal == null) return false;
        modal.OnDismissRequested?.Invoke(modal);
        return true;
    }

    public void LoadProjectFile(string location)
    {
        try
        {
            State.LoadProjectFromFile(location);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load project \"{location}\": {e.Message}");
        }
    }

    /// <summary>Saves to the known path, or asks for one; runs the continuation only on success.</summary>
    private void SaveProject(Action? andThen = null)
    {
        if (State.ProjectPath is { } path)
        {
            if (WriteProject(path)) andThen?.Invoke();
            return;
        }

        ShowFileDialog($"{State.Project.Info.Name}.tdwproj", ".tdwproj", picked =>
        {
            if (WriteProject(picked)) andThen?.Invoke();
        });
    }

    private bool WriteProject(string path)
    {
        try
        {
            State.SaveProjectToFile(path);
            RefreshTitle(); // saving clears Dirty without firing OnProjectChanged
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to save project \"{path}\": {e.Message}");
            return false;
        }
    }

    /// <summary>Timestamped snapshot next to the executable — doesn't touch ProjectPath/Dirty,
    /// so it's invisible to the normal save flow (only Update's timer drives it).</summary>
    private void WriteBackup()
    {
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            var name = string.Concat(State.Project.Info.Name.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{name}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.tdwproj";
            File.WriteAllText(Path.Combine(BackupDirectory, fileName), ProjectFile.Save(State.Project));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to write backup: {e.Message}");
        }
    }

    /// <summary>Back button / Escape: leaves directly when clean, otherwise asks first.</summary>
    public void RequestBack()
    {
        if (!State.Dirty)
        {
            PerformBack();
            return;
        }

        var save = new Button(_context, "Save") { FontSizePx = 14 };
        var discard = new Button(_context, "Discard") { FontSizePx = 14 };
        var cancel = new Button(_context, "Cancel") { FontSizePx = 14 };
        var content = new FlexPanel(_context)
        {
            Direction = LayoutDirection.Vertical,
            Width = 400,
            Padding = 14,
            Spacing = 12,
            Background = new ColoredPlane { Color = new Vector4(0.086f, 0.086f, 0.118f, 1f) },
            Children =
            [
                new Label(_context, "Unsaved changes — save before leaving?") { FontSizePx = 15f },
                new FlexPanel(_context)
                {
                    Width = LiteralOrComputable.Percent(100),
                    Height = 44,
                    Spacing = 10,
                    HorizontalAlign = Align.End,
                    VerticalAlign = Align.Center,
                    Children = [save, discard, cancel]
                }
            ]
        };
        var modal = ShowModal(content);
        save.OnClick = _ =>
        {
            RootPanel.RemoveChild(modal);
            SaveProject(PerformBack);
        };
        discard.OnClick = _ =>
        {
            RootPanel.RemoveChild(modal);
            PerformBack();
        };
        cancel.OnClick = _ => RootPanel.RemoveChild(modal);
    }

    private void PerformBack()
    {
        if (_titleActive) _workflow.Game.Title = _defaultTitle;
        _titleActive = false;
        OnBack();
    }

    /// <summary>Called when the editor scene becomes the active one.</summary>
    public void SceneShown()
    {
        _titleActive = true;
        RefreshTitle();
    }

    private void RefreshTitle()
    {
        if (!_titleActive) return;
        _workflow.Game.Title = $"{State.Project.Info.Name}{(State.Dirty ? " •" : "")} — {_defaultTitle}";
    }

    private ModalLayer ShowModal(UIElement content)
    {
        var modal = new ModalLayer(_context);
        modal.OnDismissRequested = m => RootPanel.RemoveChild(m);
        modal.AddChild(content);
        RootPanel.AddChild(modal);
        return modal;
    }

    /// <summary>Open (null name) or save-as (suggested name) dialog for one extension.</summary>
    private void ShowFileDialog(string? saveFileName, string extension, Action<string> onPicked)
    {
        var selection = new FileSelection(_context, saveFileName, extension)
        {
            Width = 560,
            Height = 440
        };
        var modal = ShowModal(selection);
        selection.OnSelect = _ =>
        {
            if (selection.SelectedPath is not { } path) return;
            RootPanel.RemoveChild(modal);
            onPicked(path);
        };
        selection.OnCancel = _ => RootPanel.RemoveChild(modal);
    }

    private void ShowExportDialog()
    {
        var dialog = new ExportDialog(_context);
        var modal = ShowModal(dialog);
        dialog.CancelButton.OnClick = _ => RootPanel.RemoveChild(modal);
        dialog.TdwButton.OnClick = _ =>
        {
            var style = dialog.Style;
            RootPanel.RemoveChild(modal);
            ShowFileDialog($"{State.Project.Info.Name}.tdw", ".tdw", path => ExportTdw(path, style));
        };
        dialog.WavButton.OnClick = _ =>
        {
            RootPanel.RemoveChild(modal);
            ShowFileDialog($"{State.Project.Info.Name}.wav", ".wav", path => Playback.ExportWav(path));
        };
    }

    private void ExportTdw(string path, SequenceStyle style)
    {
        try
        {
            File.WriteAllText(path, SequenceText.Serialize(State.Project.ToSequence(style)));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to export \"{path}\": {e.Message}");
        }
    }

    private void RefreshProject()
    {
        _projectName.SetTextContents(State.Project.Info.Name);
        _projectBpm.SetTextContents($"{State.Project.RootTiming.BPM:0.##} BPM");
        RefreshTitle();

        // Full row rebuild: the track list is small by design (the grid is what scales).
        // The add-track row is pulled out and re-appended last so it always trails the tracks.
        _trackList.RemoveChild(_addTrackRow);
        foreach (var row in _trackList.Children.OfType<EditorTrack>().ToArray())
            _trackList.RemoveChild(row);
        foreach (var track in State.Project.Tracks)
            _trackList.AddChild(new EditorTrack(_context, track, State));
        _trackList.AddChild(_addTrackRow);

        _arrangement.Refresh();
        _trackEditor.InvalidateLayout();
        _inspector.Sync();
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        foreach (var row in _trackList.Children.OfType<EditorTrack>())
            row.SetSelected(row.Track == State.SelectedTrack);
    }

    public void Resize(float width, float height)
    {
        // The window remainder isn't expressible in the stylesheet (no calc()),
        // so the body regions are sized here. The transport controls dock inside the
        // track column now (see the constructor), so no separate footer band to
        // subtract from the grid/inspector columns.
        _trackColumn.Height = height - HeaderHeight;
        _gridArea.Width = width - TrackColumnWidth - InspectorPanel.PanelWidth;
        _gridArea.Height = height - HeaderHeight;
        _inspectorColumn.X = width - InspectorPanel.PanelWidth;
        _inspectorColumn.Height = height - HeaderHeight;

        // DrawTo (not just Layout): a row whose Visible flips true here (e.g. LaneHeader's
        // M/S toggles, false on the very first pass while grid-area was still 0-height) needs
        // its DrawSelf to run at least once to ever get QueueRender'd - Layout alone recomputes
        // position/Visible but never queues a render.
        RootPanel.InvalidateCoordinates();
        RootPanel.DrawTo(_context);
    }

    public void Update(UIContext context)
    {
        Playback.Update();
        _workflow.AtlasStore.Update(); // animated sound icons advance their frames here

        if (State.Dirty && _sinceBackup.ElapsedMilliseconds >= BackupIntervalMs)
        {
            WriteBackup();
            _sinceBackup.Restart();
        }

        if (Playback.HasSession)
        {
            var elapsed = Playback.ElapsedMs;
            var total = Playback.TotalMs;
            _transportProgress.Progress = total > 0 ? (float)elapsed / total : 0;
            _elapsedLabel.SetTextContents(TimeString(elapsed));
            _totalLabel.SetTextContents(TimeString(total));
            _arrangement.PlayheadQuarters = Playback.PlayheadQuarters;
            _trackEditor.PlayheadQuarters = Playback.PlayheadQuarters;
            State.IsCurrentlyPlayingAudio = Playback.IsPlaying;
        }

        _playButton.Label.SetTextContents(Playback.IsPlaying ? "Pause" : "Play");

        RootPanel.Update(context);
        RootPanel.Layout();
    }

    private static string TimeString(long ms)
    {
        return $"{ms / 60000}:{ms / 1000 % 60:00}";
    }

    public void MouseEvent(MouseState mouseState, Vector2 scale)
    {
        RootPanel.Test(mouseState, scale);
        // The framework only routes left/right buttons; middle-drag panning is fed here.
        _trackEditor.MiddlePan(_editorOpen && mouseState.IsButtonDown(MouseButton.Middle),
            _context.PointerX, _context.PointerY);
    }
}

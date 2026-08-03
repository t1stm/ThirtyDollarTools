using EditorScene.Scenes.Components;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Scenes;

public class EditorInterface
{
    private const float HeaderHeight = 32;
    private const float TrackColumnWidth = 260;

    private readonly ArrangementView _arrangement;
    private readonly FlexPanel _arrangementPanel;
    private readonly UIContext _context;

    private readonly string _defaultTitle;
    private readonly DialogHost _dialogHost;
    private readonly FlexPanel _gridArea;
    private readonly InspectorPanel _inspector;
    private readonly Panel _inspectorColumn;
    private readonly Button _instrumentButton;
    private readonly InstrumentWorkflow _instrumentWorkflow;
    private readonly LaneHeader _laneHeader;
    private readonly TextInput _openedTrackName;
    private readonly Label _projectBpm;
    private readonly ProjectIO _projectIo;
    private readonly Label _projectName;
    private readonly ModalLayer _soundFilterModal;
    private readonly SoundPicker _soundFilterPicker;
    private readonly List<(Button Button, EditorTool Tool)> _toolButtons = [];
    private readonly Panel _trackColumn;
    private readonly TrackEditorView _trackEditor;
    private readonly FlexPanel _trackEditorPanel;
    private readonly TrackListPanel _trackList;
    private readonly TransportSection _transport;
    private readonly ThirtyDollarWorkflow _workflow;
    private TrackAutomation? _editingTrackAutomation;

    private bool _editorOpen;

    /// <summary>Scenes are constructed at startup; only the active editor owns the window title.</summary>
    private bool _titleActive;

    /// <summary>
    ///     Guards <see cref="ShowTrackContextMenu" /> against reopening on every held frame -
    ///     right-press is level-triggered, so a stationary right-click keeps firing.
    /// </summary>
    private ModalLayer? _trackContextMenuModal;

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
        _dialogHost = new DialogHost(context, RootPanel);
        _projectIo = new ProjectIO(State, _dialogHost, workflow.Logger) { OnSaved = RefreshTitle };

        var ids = Component.RegisteredIDs;
        _projectName = (Label)ids["project-name"];
        _projectBpm = (Label)ids["project-bpm"];
        _trackColumn = (Panel)ids["track-column"];
        _gridArea = (FlexPanel)ids["grid-area"];
        _inspectorColumn = (Panel)ids["inspector-column"];

        Playback = new EditorPlayback(workflow, State);

        _defaultTitle = workflow.Game.Title;
        ((Button)ids["load-button"]).OnClick = _ => _dialogHost.ShowFileDialog(null, ".tdwproj", LoadProjectFile);
        ((Button)ids["save-button"]).OnClick = _ => _projectIo.Save();
        ((Button)ids["export-button"]).OnClick = _ => ShowExportDialog();

        // Tool toggle buttons: highlighted background follows State.ActiveTool (code-owned
        // ColoredPlane, not a stylesheet class - the active highlight is a runtime toggle,
        // not a hover/press state). Draw is the default active tool. One pair per grid
        // mode (see MakeToolButton) - the arrangement bar and the editor bar each own one.
        State.OnToolChanged += tool =>
        {
            foreach (var (button, buttonTool) in _toolButtons)
            {
                var active = buttonTool == tool;
                ((ColoredPlane)button.Background!).Color = active ? ToolAccent(buttonTool) : EditorPalette.Surface;
                button.Label.Color = ToolTextColor(buttonTool, active);
            }
        };

        // The column body stacks the scrollable track list above the transport
        // controls: the list is percent-height, so it yields whatever room the
        // auto-sized transport section (below) doesn't need - no separate full-width
        // bottom bar, no magic footer-height constant to keep in sync.
        var trackColumnBody = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };
        _trackColumn.AddChild(trackColumnBody);

        _trackList = new TrackListPanel(context, State) { OnContextMenu = ShowTrackContextMenu };
        trackColumnBody.AddChild(_trackList);

        _transport = new TransportSection(context, Playback, RequestBack);
        trackColumnBody.AddChild(_transport);

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
        // The arrangement wraps in a vertical panel mirroring _trackEditorPanel below:
        // a slim bar holding the tool buttons, then the lane header + grid row.
        var arrangementBar = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 40,
            Spacing = 12,
            Padding = 6,
            Children =
            [
                new Panel(context) { Width = LiteralOrComputable.Percent(100) },
                MakeToolButton("Draw", EditorTool.Draw), MakeToolButton("Select", EditorTool.Select)
            ]
        };
        var arrangementBody = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Children = [_laneHeader, _arrangement]
        };
        _arrangementPanel = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100),
            Children = [arrangementBar, arrangementBody]
        };
        _gridArea.AddChild(_arrangementPanel);
        _arrangement.OnOpenTrack = State.OpenTrack;
        _arrangement.OnSeekQuarters = Playback.Seek;
        _arrangement.OnScrolled = _laneHeader.InvalidateLayout;

        _trackEditor = new TrackEditorView(context, State)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };
        _openedTrackName = new TextInput(context)
        {
            FontSizePx = 15f,
            Width = 220,
            BorderRadius = 4,
            Background = new ColoredPlane { Color = EditorPalette.InputBackground },
            OnValueChanged = input =>
            {
                if (State.OpenedTrack is { } track) State.RenameTrack(track, input.Value);
            }
        };

        _instrumentWorkflow = new InstrumentWorkflow(context, State, Playback, _dialogHost, workflow.AtlasStore,
            AllSounds);

        _instrumentButton = new Button(context, "Instrument: -")
        {
            OnClick = _ => _instrumentWorkflow.OpenSelector()
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
            Background = new ColoredPlane { Color = EditorPalette.Panel },
            Children = [soundFilterList, doneButton]
        };
        _soundFilterModal = new ModalLayer(context);
        _soundFilterModal.AddChild(soundFilterFrame);
        doneButton.OnClick = _ => CommitAndCloseSoundFilter();
        // Dismissing via the backdrop commits too - clicking outside shouldn't discard picks.
        _soundFilterModal.OnDismissRequested = _ => CommitAndCloseSoundFilter();

        var backToArrangement = new Button(context, "← Arrangement") { OnClick = _ => State.CloseTrack() };
        // Percent-width spacer soaks up the free space so the tool buttons land flush
        // against the bar's right edge - this framework has no space-between align.
        var editorBarSpacer = new Panel(context) { Width = LiteralOrComputable.Percent(100) };
        var editorBar = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 40,
            Spacing = 12,
            Padding = 6,
            Children =
            [
                backToArrangement, _openedTrackName, _instrumentButton, editorBarSpacer,
                MakeToolButton("Draw", EditorTool.Draw), MakeToolButton("Select", EditorTool.Select)
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
        _inspector.OnReassignInstrument = notes => _instrumentWorkflow.OpenSelector(notes);

        State.OnProjectChanged += () =>
        {
            RefreshProject();
            Playback.NotifyModelChanged();
        };
        State.OnInstrumentsChanged += RefreshActiveInstrument;
        State.OnSelectionChanged += _ =>
        {
            RefreshSelection();
            _inspector.Rebuild();
        };
        State.OnPlacementSelectionChanged += _ =>
        {
            _arrangement.RefreshSelection();
            _inspector.Rebuild();
        };
        State.OnChannelsChanged += () =>
        {
            _laneHeader.RefreshChannels();
            Playback.NotifyChannelsChanged();
        };
        State.OnOpenedTrackChanged += track =>
        {
            SwapGridView(track);
            RefreshActiveInstrument();
            _inspector.Rebuild();
        };
        State.OnNoteSelectionChanged += _ =>
        {
            _trackEditor.InvalidateLayout();
            _inspector.Rebuild();
        };
        State.OnSegmentSelectionChanged += _ =>
        {
            _trackEditor.InvalidateLayout();
            _inspector.Rebuild();
        };
        _trackEditor.OnPreviewNote = Playback.PreviewNote;
        _trackEditor.OnSeekQuarters = Playback.Seek;
        RefreshProject();
    }

    public EditorState State { get; } = new();
    public EditorPlayback Playback { get; }

    public Action OnBack { get; }
    [UsedImplicitly] public SundexComponent Component { get; }
    public Panel RootPanel { get; }

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
        _arrangement.FineSnap = shift;
        _arrangement.WheelZooms = ctrl;
        _instrumentWorkflow.SetModifiers(shift, ctrl);
    }

    private void SwapGridView(ProjectTrack? track)
    {
        if (track != null) _openedTrackName.Value = track.Name;
        if (track != null == _editorOpen) return;
        _editorOpen = track != null;

        if (_editorOpen)
        {
            _gridArea.RemoveChild(_arrangementPanel);
            _gridArea.AddChild(_trackEditorPanel);
            _trackEditor.CenterOnZero();
        }
        else
        {
            _gridArea.RemoveChild(_trackEditorPanel);
            _gridArea.AddChild(_arrangementPanel);
            _arrangement.Refresh();
        }
    }

    /// <summary>One Draw/Select toggle; every made button follows State.OnToolChanged (see ctor).</summary>
    private Button MakeToolButton(string text, EditorTool tool)
    {
        var active = State.ActiveTool == tool;
        var button = new Button(_context, text)
        {
            Background = new ColoredPlane
            {
                Color = active ? ToolAccent(tool) : EditorPalette.Surface
            },
            FontSizePx = 12,
            BorderRadius = 6,
            OnClick = _ => State.ActiveTool = tool
        };
        button.Label.Color = ToolTextColor(tool, active);
        _toolButtons.Add((button, tool));
        return button;
    }

    /// <summary>Each tool's active-highlight color: Draw blue, Select yellow.</summary>
    private static Vector4 ToolAccent(EditorTool tool)
    {
        return tool == EditorTool.Select ? EditorPalette.AccentYellow : EditorPalette.Accent;
    }

    /// <summary>Select's yellow highlight is light, so its active label needs dark text for contrast.</summary>
    private static Vector4 ToolTextColor(EditorTool tool, bool active)
    {
        return active && tool == EditorTool.Select ? EditorPalette.Panel : Vector4.One;
    }

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

    private void CommitAndCloseSoundFilter()
    {
        if (_editingTrackAutomation is { } automation)
            State.Edit(() => automation.Sounds = _soundFilterPicker.Selected.ToList());
        RootPanel.RemoveChild(_soundFilterModal);
        _inspector.Rebuild();
    }

    private void RefreshActiveInstrument()
    {
        _instrumentButton.Label.SetTextContents($"Instrument: {State.ActiveInstrument?.Name ?? "-"}");
        _instrumentButton.InvalidateLayout();
    }

    /// <summary>Dismisses the topmost open modal, if any. Used so Escape closes a dialog instead of the editor.</summary>
    public bool TryCloseTopModal()
    {
        return _dialogHost.TryCloseTop();
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
            confirmLabel: "Continue", confirmColor: EditorPalette.Accent);
    }

    /// <summary>
    ///     Shows the single-track/project/cancel choice for a dropped TDW sequence
    ///     file. Import-as-project discards the open project, so it's guarded behind the
    ///     same dirty check as every other destructive action here.
    /// </summary>
    public void ImportSequenceFile(string path)
    {
        var dialog = new ImportDialog(_context, Path.GetFileName(path));
        var modal = _dialogHost.Show(dialog);
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.SingleTrackButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            _projectIo.ImportTdw(path, ImportMode.Track, SoundMap());
        };
        dialog.ProjectButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            if (State.Dirty)
                _dialogHost.Confirm(
                    "Importing as a project discards unsaved changes.\n" +
                    "Continue?",
                    () => _projectIo.ImportTdw(path, ImportMode.Project, SoundMap()),
                    confirmLabel: "Import", confirmColor: EditorPalette.Accent);
            else
                _projectIo.ImportTdw(path, ImportMode.Project, SoundMap());
        };
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

        var dialog = new UnsavedChangesDialog(_context);
        var modal = _dialogHost.Show(dialog);
        dialog.SaveButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            _projectIo.Save(PerformBack);
        };
        dialog.DiscardButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            DiscardChanges();
            PerformBack();
        };
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
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
        _workflow.Game.Title = $"{State.Project.Info.Name}{(State.Dirty ? " •" : "")} - {_defaultTitle}";
    }

    private void ShowTrackContextMenu(ProjectTrack track)
    {
        if (_trackContextMenuModal != null) return;

        var menu = new TrackContextMenu(_context, $"{track.Name} copy");
        var modal = _dialogHost.Show(menu);
        _trackContextMenuModal = modal;
        modal.OnDismissRequested = m =>
        {
            _dialogHost.Close(m);
            _trackContextMenuModal = null;
        };
        menu.CancelButton.OnClick = _ => modal.OnDismissRequested!(modal);
        menu.DuplicateButton.OnClick = _ =>
        {
            State.DuplicateTrack(track, menu.NameInput.Value);
            modal.OnDismissRequested!(modal);
        };
    }

    private void ShowExportDialog()
    {
        var dialog = new ExportDialog(_context);
        var modal = _dialogHost.Show(dialog);
        dialog.CancelButton.OnClick = _ => _dialogHost.Close(modal);
        dialog.TdwButton.OnClick = _ =>
        {
            var style = dialog.Style;
            _dialogHost.Close(modal);
            _dialogHost.ShowFileDialog($"{State.Project.Info.Name}.tdw", ".tdw",
                path => _projectIo.ExportTdw(path, style));
        };
        dialog.WavButton.OnClick = _ =>
        {
            _dialogHost.Close(modal);
            _dialogHost.ShowFileDialog($"{State.Project.Info.Name}.wav", ".wav", path => Playback.ExportWav(path));
        };
    }

    private void RefreshProject()
    {
        _projectName.SetTextContents(State.Project.Info.Name);
        _projectBpm.SetTextContents($"{State.Project.RootTiming.BPM:0.##} BPM");
        RefreshTitle();

        _trackList.Rebuild();
        _arrangement.Refresh();
        _trackEditor.InvalidateLayout();
        _inspector.Sync();
        RefreshSelection();

        // Channel count may have just changed (import/load/undo/...), and the header
        // otherwise only relayouts from ArrangementView.OnScrolled. DrawTo (not just
        // Layout): a row whose Visible flips true here needs its DrawSelf to run at
        // least once to ever get QueueRender'd - see Resize()'s identical comment.
        // Only while the arrangement is actually attached to _gridArea, though - the
        // note editor detaches it (SwapGridView), and DrawTo queues renders regardless
        // of tree attachment, so calling this unconditionally painted orphaned M/S
        // buttons over the note editor. Reopening the arrangement redraws it anyway,
        // via AddChild's own Drawn-aware DrawTo.
        if (!_editorOpen)
        {
            _laneHeader.InvalidateLayout();
            _laneHeader.DrawTo(_context);
        }
    }

    private void RefreshSelection()
    {
        _trackList.RefreshSelection();
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
        _inspector.SetStatus(Playback.StatusLabel, Playback.StatusProgress, Playback.StatusDone, Playback.StatusTotal);
        if (Playback.TakeError() is { } error) _dialogHost.Alert(error);
        _workflow.AtlasStore.Update(); // animated sound icons advance their frames here
        _projectIo.TickBackup();

        if (Playback.HasSession)
        {
            _arrangement.PlayheadQuarters = Playback.PlayheadQuarters;
            _trackEditor.PlayheadQuarters = Playback.PlayheadQuarters;
            State.IsCurrentlyPlayingAudio = Playback.IsPlaying;
            if (Playback.IsPlaying) FollowPlayheadSegment();
        }

        _transport.Refresh();

        RootPanel.Update(context);
        RootPanel.Layout();
    }

    /// <summary>
    ///     Keeps the inspector panel live during playback: selects whichever segment of the
    ///     opened track the playhead currently sits inside, same placement lookup as
    ///     <see cref="TrackEditorView" />'s playhead line.
    /// </summary>
    private void FollowPlayheadSegment()
    {
        if (State.OpenedTrack is not { } track) return;
        var bpm = State.Project.RootTiming.BPM;
        var duration = track.DurationMinutes();
        foreach (var placement in State.Project.Placements)
        {
            if (placement.Track != track) continue;
            var localMinutes = (Playback.PlayheadQuarters - placement.StartQuarterNotes) / bpm;
            if (localMinutes < 0 || localMinutes >= duration) continue;
            if (track.SegmentAtGlobalStep((int)track.StepPositionAt(localMinutes)) is { } found)
                State.SelectSegment(found.Segment);
            return;
        }
    }

    public void MouseEvent(MouseState mouseState, Vector2 scale)
    {
        RootPanel.Test(mouseState, scale);
        // The framework only routes left/right buttons; middle-drag panning is fed here.
        var middle = mouseState.IsButtonDown(MouseButton.Middle);
        if (_editorOpen) _trackEditor.MiddlePan(middle, _context.PointerX, _context.PointerY);
        else _arrangement.MiddlePan(middle, _context.PointerX, _context.PointerY);
    }
}
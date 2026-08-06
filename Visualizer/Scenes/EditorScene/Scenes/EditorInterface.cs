using EditorScene.Scenes.Components;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Bars;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using Sundex.Markup.Attributes;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Layout;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes;

public class EditorInterface
{
    private const float HeaderHeight = 32;
    private const float TrackColumnWidth = 260;
    private const float HintBarHeight = 26;

    /// <summary>
    ///     The hint bar's default text: gestures and shortcuts that have no on-screen
    ///     control of their own, so nothing else in the UI hints they exist.
    /// </summary>
    private const string HintLegend =
        "Double-click a track to open it, right-click for options  •  " +
        "Ctrl+C/V/X copy/paste/cut, Ctrl+A select all  •  " +
        "Middle-drag to pan, Ctrl+scroll to zoom, Shift+drag to fine-snap";

    private readonly ArrangementView _arrangement;
    private readonly UIContext _context;

    private readonly string _defaultTitle;
    private readonly DialogHost _dialogHost;
    private readonly FlexPanel _gridArea;
    private readonly InspectorPanel _inspector;
    private readonly Panel _inspectorColumn;
    private readonly InstrumentWorkflow _instrumentWorkflow;
    private readonly LaneHeader _laneHeader;
    private readonly ProjectIO _projectIo;
    private readonly SoundPicker _soundFilterPicker;
    private readonly List<(Button Button, EditorTool Tool)> _toolButtons = [];
    private readonly Panel _trackColumn;
    private readonly TrackEditorView _trackEditor;
    private readonly TrackListPanel _trackList;
    private readonly TransportController _transport;
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

        // Everything the root document resolves by name has to exist before it is built:
        // a factory tag is looked up while the tree is constructed, and an imported
        // component name that isn't registered yet throws.
        Playback = new EditorPlayback(workflow, State);
        _trackList = new TrackListPanel(context, State) { OnContextMenu = ShowTrackContextMenu, OnHint = SetHint };
        _arrangement = new ArrangementView(context, State);
        _laneHeader = new LaneHeader(context, State, _arrangement)
        {
            // Not styled: the gutter's width is a layout constant the lane math reads,
            // not a look. Its fill is (see Panels.snx.ss's lane-header).
            Width = LaneHeader.GutterWidth,
            Height = LiteralOrComputable.Percent(100),
            OnHint = SetHint
        };
        _trackEditor = new TrackEditorView(context, State);
        _soundFilterPicker = new SoundPicker(context, workflow.AtlasStore)
            { ID = "sound-filter-picker", MultiSelect = true };

        // Built here rather than inside the factories because LaneHeader takes the
        // ArrangementView and factories fire in document order, with <lane-header/> ahead
        // of <arrangement-view/>.
        // ponytail: pass-through, so the same tag used twice would reparent the one
        // instance. Build inside the factory if a second usage site ever becomes real.
        sundexContext.RegisterElementFactory("track-list", _ => _trackList);
        sundexContext.RegisterElementFactory("arrangement-view", _ => _arrangement);
        sundexContext.RegisterElementFactory("lane-header", _ => _laneHeader);
        sundexContext.RegisterElementFactory("track-editor-view", _ => _trackEditor);
        sundexContext.RegisterElementFactory("sound-picker", _ => _soundFilterPicker);
        foreach (var name in new[]
                 {
                     "Layout/Editor Header/EditorHeader",
                     "Layout/Transport Section/TransportSection",
                     "Layout/Hint Bar/HintBar",
                     "Layout/Arrangement Panel/ArrangementPanel",
                     "Layout/Track Editor Panel/TrackEditorPanel",
                     "Layout/Inspector Shell/InspectorShell",
                     "Dialogs/Sound Filter/SoundFilter"
                 })
            sundexContext.NewComponent(LoadMarkup(context, $"Scenes/{name}.snx.xml"));

        Component = sundexContext.NewComponent(LoadMarkup(context, "Scenes/Layout/Editor Interface/EditorInterface.snx.xml"));
        RootPanel = Component.Element as Panel ?? throw new Exception("Root panel not found");

        var ids = Component.RegisteredIDs;
        _trackColumn = (Panel)ids["track-column"];
        _gridArea = (FlexPanel)ids["grid-area"];
        _inspectorColumn = (Panel)ids["inspector-column"];

        // Fired by AdoptToolButton's registrations, which the two grid panels' logic makes
        // below - the handler reads _toolButtons per fire, so order does not matter.
        State.OnToolChanged += tool =>
        {
            foreach (var (button, buttonTool) in _toolButtons)
            {
                var active = buttonTool == tool;
                ((ColoredPlane)button.Background!).Color = active ? ToolAccent(buttonTool) : EditorPalette.Surface;
                button.Label.Color = ToolTextColor(buttonTool, active);
            }
        };

        // Runs every component's logic depth-first, then verifies that no [SetFromLogic]
        // property was left null. The getters catch one nulled back out during logic.
        sundexContext.RunLogicAndVerify(Component,
            () => ProjectName, () => ProjectBpm,
            () => TransportProgress, () => TransportElapsed, () => TransportTotal, () => TransportPlay,
            () => HintBar, () => HintGutter, () => HintLabel,
            () => ArrangementPanel, () => TrackEditorPanel, () => OpenedTrackName, () => InstrumentButton,
            () => InspectorPanelElement, () => InspectorRows, () => InspectorStatusBar, () => InspectorStatusLabel,
            () => SoundFilterModal);
        _transport = new TransportController(Playback,
            TransportProgress, TransportElapsed, TransportTotal, TransportPlay);
        HintLabel.SetTextContents(HintLegend);
        AlignHintToGrid();

        // Both panels are in the markup so one pass resolves their handles; only the
        // current one stays attached. Detached before the first DrawTo so the note editor
        // is never drawn without an opened track, exactly as when it was code-built.
        _gridArea.RemoveChild(TrackEditorPanel);
        RootPanel.RemoveChild(SoundFilterModal);

        RootPanel.DrawTo(context);
        _dialogHost = new DialogHost(context, RootPanel);
        _projectIo = new ProjectIO(State, _dialogHost, workflow.Logger) { OnSaved = RefreshTitle };

        _defaultTitle = workflow.Game.Title;

        _arrangement.OnOpenTrack = State.OpenTrack;
        _arrangement.OnSeekQuarters = Playback.Seek;
        _arrangement.OnScrolled = _laneHeader.InvalidateLayout;

        _instrumentWorkflow = new InstrumentWorkflow(context, State, Playback, _dialogHost, workflow.AtlasStore,
            AllSounds);

        // The shell is already in the tree (root markup); this only drives it.
        _inspector = new InspectorPanel(context, State,
            InspectorPanelElement, InspectorRows, InspectorStatusBar, InspectorStatusLabel);
        _inspector.OnEditTrackAutomationSounds = automation =>
        {
            EnsureSoundFilterItems();
            _editingTrackAutomation = automation;
            _soundFilterPicker.SetSelected(automation.Sounds ?? []);
            RootPanel.AddChild(SoundFilterModal);
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

    // Assigned by each region's .snx.csx. They live flat on this class rather than on the
    // controllers because RunLogicAndVerify only reflects over the one target type - a
    // nested object's properties are never checked.
    [SetFromLogic] public Label ProjectName { get; set; } = null!;
    [SetFromLogic] public Label ProjectBpm { get; set; } = null!;

    [SetFromLogic] public ProgressBar TransportProgress { get; set; } = null!;
    [SetFromLogic] public Label TransportElapsed { get; set; } = null!;
    [SetFromLogic] public Label TransportTotal { get; set; } = null!;
    [SetFromLogic] public Button TransportPlay { get; set; } = null!;

    [SetFromLogic] public FlexPanel HintBar { get; set; } = null!;
    [SetFromLogic] public Panel HintGutter { get; set; } = null!;
    [SetFromLogic] public Label HintLabel { get; set; } = null!;

    [SetFromLogic] public ModalLayer SoundFilterModal { get; set; } = null!;

    [SetFromLogic] public Panel InspectorPanelElement { get; set; } = null!;
    [SetFromLogic] public ScrollView InspectorRows { get; set; } = null!;
    [SetFromLogic] public ProgressBar InspectorStatusBar { get; set; } = null!;
    [SetFromLogic] public Label InspectorStatusLabel { get; set; } = null!;

    [SetFromLogic] public FlexPanel ArrangementPanel { get; set; } = null!;
    [SetFromLogic] public FlexPanel TrackEditorPanel { get; set; } = null!;
    [SetFromLogic] public TextInput OpenedTrackName { get; set; } = null!;
    [SetFromLogic] public Button InstrumentButton { get; set; } = null!;

    /// <summary>Loads an embedded markup/script asset by its project-relative path.</summary>
    private static string LoadMarkup(UIContext context, string location)
    {
        return context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = location }
        }).Value;
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
        _arrangement.FineSnap = shift;
        _arrangement.WheelZooms = ctrl;
        _instrumentWorkflow.SetModifiers(shift, ctrl);
    }

    private void SwapGridView(ProjectTrack? track)
    {
        if (track != null) OpenedTrackName.Value = track.Name;
        if (track != null == _editorOpen) return;
        _editorOpen = track != null;

        if (_editorOpen)
        {
            _gridArea.RemoveChild(ArrangementPanel);
            _gridArea.AddChild(TrackEditorPanel);
            _trackEditor.CenterOnZero();
        }
        else
        {
            _gridArea.RemoveChild(TrackEditorPanel);
            _gridArea.AddChild(ArrangementPanel);
            _arrangement.Refresh();
        }

        AlignHintToGrid();
    }

    /// <summary>
    ///     Indents the hint text past the active view's gutter - the arrangement's M/S lane
    ///     header, the note editor's narrower value ruler - so it starts at the first grid
    ///     column. The bar's own padding already covers part of that offset.
    /// </summary>
    private void AlignHintToGrid()
    {
        var gutter = _editorOpen ? TrackEditorView.GutterWidth : LaneHeader.GutterWidth;
        HintGutter.Width = Math.Max(0, gutter - HintBar.Padding);
    }

    /// <summary>
    ///     Takes over a markup-built Draw/Select toggle; every adopted button then follows
    ///     State.OnToolChanged (see ctor). The fill stays code-owned - it tracks
    ///     State.ActiveTool, and a stylesheet `background` would be swapped out from under
    ///     it on the next hover, which is why `class tool-button` sets no background.
    ///     Public because the two grid panels' .snx.csx call it, and a script only reaches
    ///     the public surface - EditorPalette is internal, so the tint cannot live there.
    /// </summary>
    [UsedImplicitly]
    public void AdoptToolButton(Button button, EditorTool tool)
    {
        var active = State.ActiveTool == tool;
        button.Background = new ColoredPlane { Color = active ? ToolAccent(tool) : EditorPalette.Surface };
        button.Label.Color = ToolTextColor(tool, active);
        button.OnClick = _ => State.ActiveTool = tool;
        _toolButtons.Add((button, tool));
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

    /// <summary>Public because SoundFilter.snx.csx wires both the Done button and the backdrop dismiss to it.</summary>
    public void CommitAndCloseSoundFilter()
    {
        if (_editingTrackAutomation is { } automation)
            State.Edit(() => automation.Sounds = _soundFilterPicker.Selected.ToList());
        RootPanel.RemoveChild(SoundFilterModal);
        _inspector.Rebuild();
    }

    private void RefreshActiveInstrument()
    {
        InstrumentButton.Label.SetTextContents($"Instrument: {State.ActiveInstrument?.Name ?? "-"}");
        InstrumentButton.InvalidateLayout();
    }

    /// <summary>Wired from TrackEditorPanel.snx.csx - the workflow only exists once the root is drawn.</summary>
    public void OpenInstrumentSelector()
    {
        _instrumentWorkflow.OpenSelector();
    }

    /// <summary>
    ///     Shows contextual text in the hint bar, or reverts to the static legend when
    ///     <paramref name="text" /> is null (hover exit). Wired into every control whose
    ///     purpose isn't obvious at a glance (see EditorTrack/LaneHeader's OnHint).
    /// </summary>
    private void SetHint(string? text)
    {
        HintLabel.SetTextContents(text ?? HintLegend);
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
        var modal = _dialogHost.Show(dialog.Element);
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
        var modal = _dialogHost.Show(dialog.Element);
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
        var modal = _dialogHost.Show(menu.Element);
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

    /// <summary>Load/Save, wired from EditorHeader.snx.csx - a .csx only reaches the public surface.</summary>
    public void ShowLoadDialog()
    {
        _dialogHost.ShowFileDialog(null, ".tdwproj", LoadProjectFile);
    }

    public void SaveProject()
    {
        _projectIo.Save();
    }

    public void ShowExportDialog()
    {
        var dialog = new ExportDialog(_context);
        var modal = _dialogHost.Show(dialog.Element);
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
        ProjectName.SetTextContents(State.Project.Info.Name);
        ProjectBpm.SetTextContents($"{State.Project.RootTiming.BPM:0.##} BPM");
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
        // The hint bar only spans the grid area - the track column and inspector run
        // full height beside it.
        var gridWidth = width - TrackColumnWidth - InspectorPanel.PanelWidth;
        _trackColumn.Height = height - HeaderHeight;
        _gridArea.Width = gridWidth;
        _gridArea.Height = height - HeaderHeight - HintBarHeight;
        _inspectorColumn.X = width - InspectorPanel.PanelWidth;
        _inspectorColumn.Height = height - HeaderHeight;
        HintBar.Width = gridWidth;
        HintBar.Y = height - HintBarHeight;

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
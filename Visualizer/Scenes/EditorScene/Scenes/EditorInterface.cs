using System.Globalization;
using EditorScene.Scenes.Components;
using EditorScene.State;
using JetBrains.Annotations;
using System.Collections.Concurrent;
using System.Diagnostics;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Bars;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Markup;
using Sundex.Markup.Attributes;
using Serilog.Extensions.Logging;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Dialogs;
using EditorScene.Scenes.Layout;
using EditorScene.Scenes.Views;
using VisualizerScene;
using VisualizerScene.Objects.Playfield;

namespace EditorScene.Scenes;

public partial class EditorInterface
{
    private const float HeaderHeight = 40;
    private const float TrackColumnWidth = 260;

    private readonly ArrangementView _arrangement;
    private readonly UIContext _context;

    private readonly string _defaultTitle;
    private readonly DialogHost _dialogHost;
    private readonly FlexPanel _gridArea;
    private readonly HintBarController _hint;
    private readonly InspectorPanel _inspector;
    private readonly Panel _inspectorColumn;
    private readonly InstrumentWorkflow _instrumentWorkflow;
    private readonly LaneHeader _laneHeader;
    private readonly PlayfieldSettings _playfieldLook;
    private readonly ProjectIO _projectIo;
    private readonly EditorHeaderController _header;
    private readonly SoundPicker _soundFilterPicker;
    private readonly ToolSwitch _toolSwitch;
    private readonly Panel _trackColumn;
    private readonly TrackEditorView _trackEditor;
    private readonly TrackListPanel _trackList;
    private readonly FaithfulPalette _faithfulPalette;
    private readonly FaithfulSequence _faithfulSequence;
    private readonly TransportController _transport;
    private readonly ThirtyDollarWorkflow _workflow;
    private TrackTypeDialog? _trackTypeDialog;
    private TrackAutomation? _editingTrackAutomation;

    /// <summary>
    ///     Which panel is attached to the grid area: null is the arrangement, otherwise the
    ///     opened track's editor - <see cref="TrackEditorPanel" /> or
    ///     <see cref="FaithfulPanel" />, whichever the track's kind calls for.
    /// </summary>
    private FlexPanel? _openPanel;

    /// <summary>Scenes are constructed at startup; only the active editor owns the window title.</summary>
    private bool _titleActive;

    public EditorInterface(UIContext context, ThirtyDollarWorkflow workflow, Action back)
    {
        _context = context;
        _workflow = workflow;
        OnBack = back;
        State = new EditorState { Logger = new SerilogLoggerFactory(workflow.Logger).CreateLogger("Editor") };

        var sundexContext = new SundexContext(context);

        // Everything the root document resolves by name has to exist before it is built:
        // a factory tag is looked up while the tree is constructed, and an imported
        // component name that isn't registered yet throws.
        Playback = new EditorPlayback(workflow, State);
        // Ahead of the track list, which resolves its row blips through the arrangement's palette.
        _arrangement = new ArrangementView(context, State);
        _trackList = new TrackListPanel(context, State)
        {
            OnContextMenu = ShowTrackContextMenu, OnHint = SetHint,
            TrackColor = track => _arrangement.ColorOf(track) // the blip shows the resting fill, never the selected lift
        };
        _laneHeader = new LaneHeader(context, State, _arrangement)
        {
            // Not styled: the gutter's width is a layout constant the lane math reads,
            // not a look. Its fill is (see Panels.snx.ss's lane-header).
            Width = LaneHeader.GutterWidth,
            Height = LiteralOrComputable.Percent(100),
            OnHint = SetHint
        };
        _trackEditor = new TrackEditorView(context, State);
        _playfieldLook = PlayfieldLook(workflow);
        var faithfulScale = new FaithfulScale();
        _faithfulPalette = new FaithfulPalette(context, State, _playfieldLook, faithfulScale);
        _faithfulSequence = new FaithfulSequence(context, State, _playfieldLook, faithfulScale);
        // The sequence works the size out from its own width; the palettes are told, since
        // nothing about their rectangles changes when it does.
        faithfulScale.Changed += _faithfulPalette.RefreshScale;
        _soundFilterPicker = new SoundPicker(context, workflow.AtlasStore)
            { ID = "sound-filter-picker", MultiSelect = true };
        _toolSwitch = new ToolSwitch(context) { OnPick = SelectTool, OnHint = SetHint };
        _toolSwitch.SetActive(State.ActiveTool);

        // Built here rather than inside the factories because LaneHeader takes the
        // ArrangementView and factories fire in document order, with <lane-header/> ahead
        // of <arrangement-view/>.
        // ponytail: pass-through, so the same tag used twice would reparent the one
        // instance. Build inside the factory if a second usage site ever becomes real.
        sundexContext.RegisterElementFactory("track-list", _ => _trackList);
        sundexContext.RegisterElementFactory("arrangement-view", _ => _arrangement);
        sundexContext.RegisterElementFactory("lane-header", _ => _laneHeader);
        sundexContext.RegisterElementFactory("track-editor-view", _ => _trackEditor);
        sundexContext.RegisterElementFactory("faithful-palette", _ => _faithfulPalette);
        sundexContext.RegisterElementFactory("faithful-sequence", _ => _faithfulSequence);
        sundexContext.RegisterElementFactory("sound-picker", _ => _soundFilterPicker);
        foreach (var name in new[]
                 {
                     "Layout/Editor Header/EditorHeader",
                     "Layout/Transport Section/TransportSection",
                     "Layout/Hint Bar/HintBar",
                     "Layout/Arrangement Panel/ArrangementPanel",
                     "Layout/Track Editor Panel/TrackEditorPanel",
                     "Layout/Faithful Panel/FaithfulPanel",
                     "Layout/Inspector Shell/InspectorShell",
                     "Dialogs/Sound Filter/SoundFilter"
                 })
            sundexContext.NewComponent(Markup.Load(context, $"Scenes/{name}.snx.xml"));

        Component = sundexContext.NewComponent(Markup.Load(context, "Scenes/Layout/Editor Interface/EditorInterface.snx.xml"));
        RootPanel = Component.Element as Panel ?? throw new Exception("Root panel not found");

        var ids = Component.RegisteredIDs;
        _trackColumn = (Panel)ids["track-column"];
        _gridArea = (FlexPanel)ids["grid-area"];
        _inspectorColumn = (Panel)ids["inspector-column"];

        // Every change of tool, whichever way it came, shows the new one's name for a moment.
        State.OnToolChanged += tool =>
        {
            _toolSwitch.SetActive(tool);
            _toolSwitch.Peek(tool);
        };

        // Runs every component's logic depth-first, then verifies that no [SetFromLogic]
        // property was left null. The getters catch one nulled back out during logic.
        sundexContext.RunLogicAndVerify(Component,
            () => Header,
            () => TransportProgress, () => TransportElapsed, () => TransportTotal, () => TransportPlay,
            () => HintBar, () => HintGutter, () => HintLines, () => HintLabel,
            () => ArrangementPanel, () => TrackEditorPanel, () => OpenedTrackName, () => InstrumentButton,
            () => FaithfulPanel, () => FaithfulBody, () => FaithfulTrackName,
            () => InspectorPanelElement, () => InspectorRows, () => InspectorStatusBar, () => InspectorStatusLabel,
            () => SoundFilterModal);
        _transport = new TransportController(Playback,
            TransportProgress, TransportElapsed, TransportTotal, TransportPlay);
        _hint = new HintBarController(context, HintBar, HintGutter, HintLines, HintLabel, _gridArea, HeaderHeight);
        _hint.SetView(LaneHeader.GutterWidth, false); // Resize re-lays it once the window size is known
        _header = new EditorHeaderController(Header, State, _toolSwitch);

        // Both panels are in the markup so one pass resolves their handles; only the
        // current one stays attached. Detached before the first DrawTo, so the note editor
        // is never drawn without an opened track.
        _gridArea.RemoveChild(TrackEditorPanel);
        _gridArea.RemoveChild(FaithfulPanel);
        RootPanel.RemoveChild(SoundFilterModal);

        RootPanel.DrawTo(context);
        _dialogHost = new DialogHost(context, RootPanel);
        _projectIo = new ProjectIO(State, _dialogHost, State.Logger) { OnSaved = RefreshTitle };

        _defaultTitle = workflow.Game.Title;

        _trackList.OnAddTrack = ShowTrackTypeDialog;
        _faithfulPalette.OnHint = SetHint;
        _faithfulPalette.OnPickInstrument = instrument =>
        {
            if (State.OpenedFaithfulTrack is { } track) State.AppendItem(track, FaithfulItem.Sound(instrument));
        };
        _faithfulPalette.OnPreviewInstrument = Playback.PreviewInstrument;
        _faithfulPalette.OnPickAction = ShowActionValueDialog;
        _faithfulSequence.OnHint = SetHint;
        _faithfulSequence.OnPreviewNote = note => Playback.PreviewNote(note);
        _faithfulSequence.OnEditAction = ShowActionEditDialog;
        _faithfulSequence.OnEditCut = ShowCutTargetDialog;
        _arrangement.OnOpenTrack = State.OpenTrack;
        _arrangement.OnSeekQuarters = Playback.Seek;
        _arrangement.OnScrolled = _laneHeader.InvalidateLayout;

        _instrumentWorkflow = new InstrumentWorkflow(context, State, Playback, _dialogHost, workflow.AtlasStore,
            AllSounds);
        // The palette's "modify instruments" entry opens the instrument selector.
        _faithfulPalette.OnModifyInstruments = () => _instrumentWorkflow.OpenSelector();

        // The shell is already in the tree (root markup); this only drives it.
        _inspector = new InspectorPanel(context, State,
            InspectorPanelElement, InspectorRows, InspectorStatusBar, InspectorStatusLabel)
            {
                OnEditTrackAutomationSounds = automation =>
                {
                    EnsureSoundFilterItems();
                    _editingTrackAutomation = automation;
                    _soundFilterPicker.SetSelected(automation.Sounds ?? []);
                    RootPanel.AddChild(SoundFilterModal);
                },
                OnReassignInstrument = notes => _instrumentWorkflow.OpenSelector(notes),
                OnEditCut = ShowCutTargetDialog,
                OnChangeTrackColor = ShowTrackColorDialog,
                OnReplaceWaveFile = ShowWaveReplaceDialog,
                TrackColor = track => _arrangement.ColorOf(track) // the chip shows the resting fill, never the selected lift
            };

        State.OnProjectChanged += () =>
        {
            RefreshProject();
            Playback.NotifyModelChanged();
        };
        State.OnInstrumentsChanged += RefreshActiveInstrument;
        State.OnSelectionChanged += _ =>
        {
            _trackList.RefreshSelection();
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
        State.OnItemSelectionChanged += _ => _inspector.Rebuild();
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
        _arrangement.WavePeaks = Playback.WavePeaks;
        // A decode lands long after the clip was laid out, and nothing else would invalidate
        // it - the envelope would sit invisible until the user happened to scroll.
        Playback.OnWaveDecoded = _arrangement.InvalidateLayout;
        _trackEditor.OnPreviewNote = Playback.PreviewNote;
        _trackEditor.OnSeekQuarters = Playback.Seek;
        RefreshProject();
    }

    /// <summary>Model edits handed back from background work; drained in <see cref="Update" />.</summary>
    private readonly ConcurrentQueue<Action> _pendingEdits = new();

    public EditorState State { get; }
    public EditorPlayback Playback { get; }

    private Action OnBack { get; }
    [UsedImplicitly] public SundexComponent Component { get; }
    public Panel RootPanel { get; }

    /// <summary>
    ///     Scene-wide opacity, driven from 0 to 1 by the loading screen when the boot hands
    ///     off here (<c>--mode editor</c>). 1 on every later entry. Same deal as
    ///     HomeScene.Scenes.HomeInterface.Alpha, down to why it is re-applied every frame
    ///     while it is under 1.
    /// </summary>
    public float Alpha { get; set; } = 1f;

    private readonly ElementAlpha _alpha = new();
    private float _appliedAlpha = 1f;

    // Assigned by each region's .snx.csx. They live flat on this class rather than on the
    // controllers because RunLogicAndVerify only reflects over the one target type - a
    // nested object's properties are never checked.
    /// <summary>The top bar's document; <see cref="EditorHeaderController" /> takes its handles from it.</summary>
    [SetFromLogic] public SundexComponent Header { get; set; } = null!;

    [SetFromLogic] public ProgressBar TransportProgress { get; set; } = null!;
    [SetFromLogic] public Label TransportElapsed { get; set; } = null!;
    [SetFromLogic] public Label TransportTotal { get; set; } = null!;
    [SetFromLogic] public Button TransportPlay { get; set; } = null!;

    [SetFromLogic] public FlexPanel HintBar { get; set; } = null!;
    [SetFromLogic] public Panel HintGutter { get; set; } = null!;
    [SetFromLogic] public FlexPanel HintLines { get; set; } = null!;
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

    [SetFromLogic] public FlexPanel FaithfulPanel { get; set; } = null!;

    /// <summary>The faithful panel's content area - the palettes and sequence view mount here.</summary>
    [SetFromLogic] public FlexPanel FaithfulBody { get; set; } = null!;

    [SetFromLogic] public TextInput FaithfulTrackName { get; set; } = null!;

    /// <summary>
    ///     The playfield look the faithful views draw with: the same atlases, fonts and badge
    ///     conventions the visualizer uses, at a size that suits a panel rather than a screen.
    ///     Render scale stays 1 - these live in UI pixels, like every other element.
    /// </summary>
    private static PlayfieldSettings PlayfieldLook(ThirtyDollarWorkflow workflow)
    {
        return new PlayfieldSettings
        {
            SampleHolder = workflow.SampleHolder ?? throw new Exception("SampleHolder is null"),
            AtlasStore = workflow.AtlasStore,
            PlayfieldSizing = new PlayfieldSizing(40),
            RenderScale = 1f,
            Fonts = Visualizer.VisualizerFonts
        };
    }

    private void SwapGridView(ProjectTrack? track)
    {
        var next = track switch
        {
            null => null,
            FaithfulTrack => FaithfulPanel,
            _ => TrackEditorPanel
        };

        if (track != null) (next == FaithfulPanel ? FaithfulTrackName : OpenedTrackName).Value = track.Name;
        if (next == _openPanel)
        {
            // Same panel, another track: the note editor re-reads the opened track on every
            // layout, but the sequence caches its expanded stream and has to be told.
            if (next == FaithfulPanel) _faithfulSequence.Refresh();
            return;
        }

        _gridArea.RemoveChild(_openPanel ?? (UIElement)ArrangementPanel);
        _gridArea.AddChild(next ?? (UIElement)ArrangementPanel);
        _openPanel = next;

        if (next == TrackEditorPanel) _trackEditor.CenterOnZero();
        else if (next == FaithfulPanel)
        {
            _faithfulPalette.Rebuild(); // first chance the atlases are loaded
            _faithfulSequence.Refresh();
        }
        else _arrangement.Refresh();

        // The faithful panel has no gutter at all, so its hint starts at the panel's own padding.
        var gutter = next == TrackEditorPanel ? TrackEditorView.GutterWidth : next != null ? 0 : LaneHeader.GutterWidth;
        _hint.SetView(gutter, next == FaithfulPanel); // the legend differs per view
    }

    /// <summary>The note editor specifically - the only view with a grid to pan and a value ruler.</summary>
    private bool NoteEditorOpen => _openPanel == TrackEditorPanel;

    /// <summary>
    ///     A click on the switch or the tool's key. Picking the tool that is already active
    ///     still shows its name - nothing changes, so the change handler would not.
    /// </summary>
    public void SelectTool(EditorTool tool)
    {
        if (State.ActiveTool == tool) _toolSwitch.Peek(tool);
        else State.ActiveTool = tool;
    }

    /// <summary>
    ///     Takes over the faithful sequence's "Follow scroll" toggle. No fill in either state -
    ///     the label's color is the whole signal, muted off and the editor's blue on.
    /// </summary>
    [UsedImplicitly]
    public void AdoptFollowButton(Button button)
    {
        button.OnClick = _ =>
        {
            _faithfulSequence.FollowScroll = !_faithfulSequence.FollowScroll;
            button.Label.SetClass("follow-label-active", _faithfulSequence.FollowScroll);
        };
    }

    /// <summary>
    ///     How much of an instrument's name the tool bar's button shows. The button is the
    ///     bar's only auto-width child, so an untruncated name grows the bar without bound and
    ///     pushes itself over the inspector. The full name is in the selector
    ///     this button opens.
    /// </summary>
    private const int InstrumentNameLimit = 14;

    private void RefreshActiveInstrument()
    {
        var name = State.ActiveInstrument?.Name ?? "-";
        if (name.Length > InstrumentNameLimit) name = name[..InstrumentNameLimit] + "…";
        InstrumentButton.Label.SetTextContents($"Instrument: {name}");
        InstrumentButton.InvalidateLayout();
    }

    /// <summary>See <see cref="HintBarController.Show" />.</summary>
    private void SetHint(string? text)
    {
        _hint.Show(text);
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

    private void RefreshProject()
    {
        _header.RefreshProject(State.Project.Info.Name, string.Create(CultureInfo.InvariantCulture, $"{State.Project.RootTiming.BPM:0.##} BPM"));
        RefreshTitle();

        _trackList.Rebuild();
        _faithfulPalette.Rebuild();
        if (_openPanel == FaithfulPanel) _faithfulSequence.Refresh();

        // Only while it is the view being shown: a clip's width is its track's duration, and
        // for a faithful track working that out walks the whole sequence. SwapGridView
        // refreshes it on the way back in.
        if (_openPanel is null) _arrangement.Refresh();
        _trackEditor.InvalidateLayout();
        _inspector.Sync();
        _trackList.RefreshSelection();

        // Channel count may have just changed (import/load/undo/...), and the header
        // otherwise only relayouts from ArrangementView.OnScrolled. DrawTo (not just
        // Layout): a row whose Visible flips true here needs its DrawSelf to run at
        // least once to ever get QueueRender'd - see Resize()'s identical comment.
        // Only while the arrangement is attached to _gridArea, though: the note editor
        // detaches it (SwapGridView) and DrawTo queues renders regardless of tree
        // attachment, so doing it unconditionally paints orphaned M/S buttons over the
        // note editor. Reopening the arrangement redraws it via AddChild's own
        // Drawn-aware DrawTo.
        if (_openPanel is null)
        {
            _laneHeader.InvalidateLayout();
            _laneHeader.DrawTo(_context);
        }

    }

    public void Resize(float width, float height)
    {
        // The window remainder isn't expressible in the stylesheet (no calc()), so the body
        // regions are sized here. The transport controls dock inside the track column, so
        // there is no footer band to subtract from the grid/inspector columns.
        // The hint bar only spans the grid area - the track column and inspector run
        // full height beside it.
        var gridWidth = width - TrackColumnWidth - InspectorPanel.PanelWidth;
        _trackColumn.Height = height - HeaderHeight;
        _gridArea.Width = gridWidth;
        _inspectorColumn.X = width - InspectorPanel.PanelWidth;
        _inspectorColumn.Height = height - HeaderHeight;
        _hint.Resize(gridWidth, height);
        // Centred on the grid area, the view the tools act on; anchored on its own centre.
        _toolSwitch.X = TrackColumnWidth + gridWidth / 2;
        _toolSwitch.Y = HeaderHeight / 2;

        // DrawTo (not just Layout): a row whose Visible flips true here (e.g. LaneHeader's
        // M/S toggles, false on the very first pass while grid-area was still 0-height) needs
        // its DrawSelf to run at least once to ever get QueueRender'd - Layout alone recomputes
        // position/Visible but never queues a render.
        RootPanel.InvalidateCoordinates();
        RootPanel.DrawTo(_context);
    }

    public void Update(UIContext context)
    {
        // Model edits that finished on a background thread (a wave file's decode) are applied
        // here: the update thread is the only one allowed to touch the project.
        while (_pendingEdits.TryDequeue(out var edit)) edit();

        Playback.Update();
        _inspector.Update();
        // a render's progress wins; otherwise, while playing, how hard the mixer works
        if (Playback.StatusLabel == null && Playback.CpuLoad is { } load) _inspector.SetLoad(load);
        else _inspector.SetStatus(Playback.StatusLabel, Playback.StatusProgress, Playback.StatusDone, Playback.StatusTotal);
        if (Playback.TakeError() is { } error) _dialogHost.Alert(error);
        _workflow.AtlasStore.Update(); // animated sound icons advance their frames here
        _projectIo.TickBackup();

        if (Playback.HasSession)
        {
            _arrangement.PlayheadQuarters = Playback.PlayheadQuarters;
            _trackEditor.PlayheadQuarters = Playback.PlayheadQuarters;
            State.IsCurrentlyPlayingAudio = Playback.IsPlaying;
            if (Playback.IsPlaying) FollowPlayheadSegment();
            if (_openPanel == FaithfulPanel) _faithfulSequence.SetPlayhead(Playback.IsPlaying ? LocalPlayheadMinutes() : null);
        }

        _transport.Refresh();

        RootPanel.Update(context);
        // After the update pass, which restyles hovered elements and so resets their alpha,
        // and before layout, which has to see this frame's widths.
        _header.Update();
        _toolSwitch.Animate();
        RootPanel.Layout();
        LogDialogChanges();

        // Last, and every frame while fading: the stylesheet re-runs during the update pass
        // and puts the styled alpha straight back.
        if (Alpha >= 1f && _appliedAlpha >= 1f) return;
        _alpha.Apply(RootPanel, Alpha);
        _appliedAlpha = Alpha;
    }

    private List<ModalLayer> _openModals = [];

    /// <summary>
    ///     Logs each modal mounting and unmounting. Every way a dialog opens - DialogHost, the
    ///     instrument workflow's own layers, the sound filter, the track context menu - ends on
    ///     the root panel, so diffing its modals once a frame catches all of them. Debug builds
    ///     only, like the lines it writes.
    /// </summary>
    [Conditional("DEBUG")]
    private void LogDialogChanges()
    {
        var open = RootPanel.Children.OfType<ModalLayer>().ToList();
        foreach (var modal in open.Except(_openModals)) State.Logger.DialogOpened(DialogName(modal));
        foreach (var modal in _openModals.Except(open)) State.Logger.DialogClosed(DialogName(modal));
        _openModals = open;
    }

    /// <summary>The modal's id, else its content's (a dialog's markup root), else its type.</summary>
    private static string DialogName(ModalLayer modal)
    {
        var id = modal.ID.Length > 0 ? modal.ID : modal.Children.FirstOrDefault()?.ID;
        return string.IsNullOrEmpty(id) ? modal.GetType().Name : id;
    }

    /// <summary>
    ///     How far the playhead is into the opened track, in minutes, or null while it is
    ///     outside every placement of it. A faithful track walks its position from this;
    ///     a piano roll's <see cref="FollowPlayheadSegment" /> finds the segment under it.
    /// </summary>
    private double? LocalPlayheadMinutes()
    {
        if (State.OpenedTrack is not { } track) return null;
        var bpm = State.Project.RootTiming.BPM;
        var duration = track.DurationMinutes();

        foreach (var placement in State.Project.Placements)
        {
            if (placement.Track != track) continue;
            var localMinutes = (Playback.PlayheadQuarters - placement.StartQuarterNotes) / bpm;
            if (localMinutes < 0 || localMinutes >= duration) continue;
            return localMinutes;
        }

        return null;
    }

    /// <summary>
    ///     Keeps the inspector panel live during playback: selects whichever segment of the
    ///     opened track the playhead currently sits inside, same placement lookup as
    ///     <see cref="TrackEditorView" />'s playhead line.
    /// </summary>
    private void FollowPlayheadSegment()
    {
        // A faithful track has no segments to follow - its position is an item index.
        if (State.OpenedTrack is not { Kind: TrackKind.PianoRoll } track) return;
        if (LocalPlayheadMinutes() is { } localMinutes &&
            track.SegmentAtGlobalStep((int)track.StepPositionAt(localMinutes)) is { } found)
            State.SelectSegment(found.Segment);
    }
}

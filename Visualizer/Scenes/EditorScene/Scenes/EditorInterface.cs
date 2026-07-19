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
using Sundex.Markup.Attributes;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes;

public class EditorInterface
{
    private const float HeaderHeight = 32;
    private const float FooterHeight = 52;
    private const float TrackColumnWidth = 260;

    // Idle/hover colors for the menu-style clickable labels (top bar, add-track row).
    private static readonly Vector4 MenuTextColor = new(0.839f, 0.855f, 0.863f, 1f); // #d6dadc
    private static readonly Vector4 MenuHoverColor = new(0.478f, 0.635f, 0.969f, 1f); // #7aa2f7

    private readonly ArrangementView _arrangement;
    private readonly FlexPanel _addTrackRow;
    private readonly FlexPanel _bottomBar;
    private readonly UIContext _context;
    private readonly FlexPanel _gridArea;
    private readonly InspectorPanel _inspector;
    private readonly Panel _inspectorColumn;
    private readonly LaneHeader _laneHeader;
    private readonly TextInput _openedTrackName;
    private readonly Button _playButton;
    private readonly Label _projectBpm;
    private readonly Label _projectName;
    private readonly SoundImage _activeSoundIcon;
    private readonly Button _soundButton;
    private readonly ScrollView _soundList;
    private readonly ModalLayer _soundModal;
    private readonly SoundPicker _soundPicker;
    private readonly ModalLayer _soundFilterModal;
    private readonly SoundPicker _soundFilterPicker;
    private TrackAutomation? _editingTrackAutomation;
    private readonly Panel _trackColumn;
    private readonly TrackEditorView _trackEditor;
    private readonly FlexPanel _trackEditorPanel;
    private readonly ScrollView _trackList;
    private readonly ProgressBar _transportProgress;
    private readonly Label _transportTime;
    private readonly ThirtyDollarWorkflow _workflow;

    private readonly string _defaultTitle;

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
        sundexContext.RunLogicAndVerify(Component, () => RootPanel);

        RootPanel.DrawTo(context);

        var ids = Component.RegisteredIDs;
        _projectName = (Label)ids["project-name"];
        _projectBpm = (Label)ids["project-bpm"];
        _trackColumn = (Panel)ids["track-column"];
        _gridArea = (FlexPanel)ids["grid-area"];
        _inspectorColumn = (Panel)ids["inspector-column"];
        _transportProgress = (ProgressBar)ids["transport-progress"];
        _transportTime = (Label)ids["transport-time"];
        _playButton = (Button)ids["play-button"];
        _bottomBar = (FlexPanel)ids["bottom-bar"];

        Playback = new EditorPlayback(workflow, State);
        _playButton.OnClick = _ => Playback.PlayPause();
        ((Button)ids["stop-button"]).OnClick = _ => Playback.Stop();

        _defaultTitle = workflow.Game.Title;
        WireMenuLabel((Label)ids["load-button"], _ => ShowFileDialog(null, ".tdwproj", LoadProjectFile));
        WireMenuLabel((Label)ids["save-button"], _ => SaveProject());
        WireMenuLabel((Label)ids["export-button"], _ => ShowExportDialog());

        _trackList = new ScrollView(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };
        _trackColumn.AddChild(_trackList);

        // "Add track" lives at the end of the track list, styled like the menu
        // labels above rather than a boxed button.
        var addTrackLabel = new Label(context, "+ Add track") { FontSizePx = 14f, Color = MenuTextColor };
        _addTrackRow = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 36,
            HorizontalAlign = Align.Center,
            VerticalAlign = Align.Center,
            UpdateCursorOnHover = true,
            OnClick = _ => State.AddTrack(),
            OnHoverEnter = _ => addTrackLabel.Color = MenuHoverColor,
            OnHoverExit = _ => addTrackLabel.Color = MenuTextColor,
            Children = [addTrackLabel]
        };
        _trackList.AddChild(_addTrackRow);

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

        _trackEditor = new TrackEditorView(context, State)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };
        _openedTrackName = new TextInput(context, "")
        {
            FontSizePx = 15f,
            Width = 220,
            OnValueChanged = input =>
            {
                if (State.OpenedTrack is { } track) State.RenameTrack(track, input.Value);
            }
        };

        // The sound picker opens as a modal (add/remove on the root, the tested
        // show-hide pattern) instead of a DropDownLabel — hidden-panel toggling
        // doesn't manage the render queue. Icons come from the same atlas grid
        // DrumMaster's sound list uses.
        _soundPicker = new SoundPicker(context, workflow.AtlasStore)
        {
            Width = 640,
            OnPick = name =>
            {
                State.ActiveSound = name;
                RefreshActiveSound();
                RootPanel.RemoveChild(_soundModal!);
            }
        };
        _soundList = new ScrollView(context) { Width = 640, Height = 480 };
        _soundList.AddChild(_soundPicker);
        var soundListFrame = new Panel(context)
        {
            Width = 640,
            Height = 480,
            Background = new ColoredPlane { Color = new Vector4(0.086f, 0.086f, 0.118f, 1f) }
        };
        soundListFrame.AddChild(_soundList);
        _soundModal = new ModalLayer(context);
        _soundModal.AddChild(soundListFrame);
        _soundModal.OnDismissRequested = modal => RootPanel.RemoveChild(modal);
        _soundButton = new Button(context, "Sound: —")
        {
            OnClick = _ =>
            {
                EnsureSoundItems();
                RootPanel.AddChild(_soundModal);
            }
        };
        // The active sound shows as its image; the label is the no-image fallback.
        _activeSoundIcon = new SoundImage(context, workflow.AtlasStore) { Width = 0, Height = 0 };
        _soundButton.AddChild(_activeSoundIcon);

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
        var editorBar = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 40,
            Spacing = 12,
            Padding = 6,
            Children = [backToArrangement, _openedTrackName, _soundButton, addSegment, removeSegment]
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

        State.OnProjectChanged = () =>
        {
            RefreshProject();
            Playback.NotifyModelChanged();
        };
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
        RefreshProject();
    }

    private static void WireMenuLabel(Label label, Action<UIElement> onClick)
    {
        label.UpdateCursorOnHover = true;
        label.OnClick = onClick;
        label.OnHoverEnter = _ => label.Color = MenuHoverColor;
        label.OnHoverExit = _ => label.Color = MenuTextColor;
    }

    /// <summary>
    ///     Shift: the note editor snaps values to 0.2 instead of 1.
    ///     Ctrl: the arrangement wheel zooms instead of panning.
    /// </summary>
    public void SetModifiers(bool shift, bool ctrl)
    {
        _trackEditor.FineSnap = shift;
        _trackEditor.WheelZooms = ctrl;
        _arrangement.WheelZooms = ctrl;
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
            EnsureSoundItems();
            RefreshActiveSound();
        }
        else
        {
            _gridArea.RemoveChild(_trackEditorPanel);
            _gridArea.AddChild(_laneHeader);
            _gridArea.AddChild(_arrangement);
            _arrangement.Refresh();
        }
    }

    /// <summary>
    ///     Fills the sound picker on first open — lazily, because the sample list and
    ///     its images may still be downloading while the scene is constructed.
    /// </summary>
    private void EnsureSoundItems()
    {
        if (_soundPicker.HasSounds) return;
        _soundPicker.Fill(_workflow.SampleHolder.StringToSoundReferences.Keys.Order());
    }

    /// <summary>Same lazy-fill guard as <see cref="EnsureSoundItems" />, for the second picker.</summary>
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

    private void RefreshActiveSound()
    {
        var sound = State.ActiveSound;
        var hasImage = _activeSoundIcon.ShowSound(sound);
        _activeSoundIcon.Width = hasImage ? 26 : 0;
        _activeSoundIcon.Height = hasImage ? 26 : 0;
        _soundButton.Label.SetTextContents(hasImage ? "" : sound ?? "Sound: —");
        _soundButton.InvalidateLayout();
    }

    public EditorState State { get; } = new();
    public EditorPlayback Playback { get; }

    public Action OnBack { get; }
    [UsedImplicitly] public SundexComponent Component { get; }
    [SetFromLogic] public Panel RootPanel { get; set; } = null!;

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
        // so the body regions are sized here.
        _trackColumn.Height = height - HeaderHeight - FooterHeight;
        _gridArea.Width = width - TrackColumnWidth - InspectorPanel.PanelWidth;
        _gridArea.Height = height - HeaderHeight - FooterHeight;
        _inspectorColumn.X = width - InspectorPanel.PanelWidth;
        _inspectorColumn.Height = height - HeaderHeight - FooterHeight;
        _bottomBar.Y = height - FooterHeight;

        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public void Update(UIContext context)
    {
        Playback.Update();
        _workflow.AtlasStore.Update(); // animated sound icons advance their frames here

        if (Playback.HasSession)
        {
            var elapsed = Playback.ElapsedMs;
            var total = Playback.TotalMs;
            _transportProgress.Progress = total > 0 ? (float)elapsed / total : 0;
            _transportTime.SetTextContents($"{TimeString(elapsed)} / {TimeString(total)}");
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

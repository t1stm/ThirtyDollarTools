using Sundex.Engine.Renderer;
using JetBrains.Annotations;
using DrumMasterScene.Components;
using DrumMasterScene.UI;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Audio;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Engine;
using Sundex.Engine.AssetManagement.Extensions;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using Sundex.Markup;
using DrumMasterScene.Markup;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using VisualizerScene;
using Sundex.Engine.Input;
using Placement = ThirtyDollarConverter.Objects.Placement;

namespace DrumMasterScene;

public class DrumMaster(GameRuntime runtime, ThirtyDollarWorkflow workflow) : Scene(runtime)
{
    private readonly DollarStoreCamera _camera = new((0, 0, 0), (runtime.ClientSize.X, runtime.ClientSize.Y));

    private readonly Queue<UIElement> _deletionQueue = [];
    private readonly List<TaikoLaneConfiguration> _taikoLaneConfigurations = [];
    private readonly Visualizer _visualizer = runtime.SceneManager.Get<Visualizer>();

    private UIElement _configUI = null!;

    private UIElement? _currentUI;
    private UIElement _messageUI = null!;
    private Sequence[] _pendingSequences = [];
    private SoundList _soundList = null!;

    private DrumMasterState _state = DrumMasterState.WaitingForSequence;
    private SundexContext<DrumMaster> _sundexContext = null!;
    private ThirtyDollarWorkflow Workflow { get; } = workflow;

    private List<TaikoLane> PlayerTaikoLanes { get; } = [];

    /// <summary>Where <see cref="AddLane" /> puts the lanes. Set by SelectSounds.snx.xml.</summary>
    public FlexPanel LaneList { get; set; } = null!;

    /// <summary>Where the sound list goes: it needs the atlas store, so markup can't build it. Set by SelectSounds.snx.xml.</summary>
    public Region SoundListSlot { get; set; } = null!;

    public override void Initialize(InitArguments initArguments)
    {
        var uiContext = new UIContext
        {
            Camera = _camera
        };
        _sundexContext = new SundexContext<DrumMaster>(uiContext, this);

        _configUI = new SelectSounds(_sundexContext).Create();
        _soundList = new SoundList(uiContext, Workflow.AtlasStore) { ID = "available-sounds" };
        SoundListSlot.AddChild(_soundList);
        _configUI.Layout();

        _messageUI = new WaitingForSequence(_sundexContext).Create();
        _messageUI.Layout();

        ChangeState(DrumMasterState.WaitingForSequence);
    }

    public void AddLane()
    {
        var lane = new TaikoLaneConfiguration(_sundexContext.UIContext, _sundexContext)
        {
            OnDeleteRequested = QueueDeletion
        };
        _taikoLaneConfigurations.Add(lane);
        LaneList.AddChild(lane);
        LaneList.InvalidateLayout();
    }

    [UsedImplicitly]
    public void StartPlayback()
    {
        if (_pendingSequences.Length == 0) return;

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = 100_000,
            AddVisualEvents = false
        });

        var originalPlacements = calculator.CalculateMany(_pendingSequences).ToArray();
        foreach (var lane in PlayerTaikoLanes) lane.Dispose();

        PlayerTaikoLanes.Clear();
        var y = 100;
        foreach (var configuration in _taikoLaneConfigurations)
        {
            var sounds = configuration.SelectedSounds;
            PlayerTaikoLanes.Add(GenerateTaikoLaneFor(originalPlacements, sounds, (0, y)));
            y += 160 + 25;
        }

        ChangeState(DrumMasterState.Playing);

        Runtime.ThreadRunner.RunTask(Action);
        return;

        async void Action()
        {
            await Workflow.UpdateSequences(_pendingSequences);
        }
    }

    public override void Start()
    {
    }

    private void ChangeState(DrumMasterState state)
    {
        _state = state;
        _sundexContext.UIContext.Clear();
        _currentUI = _state switch
        {
            DrumMasterState.WaitingForSequence => _messageUI,
            DrumMasterState.Configuring => _configUI,
            DrumMasterState.Playing => null,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

        _currentUI?.DrawTo(_sundexContext.UIContext);
    }

    public override void Render(RenderArguments renderArgs)
    {
        if (_state is DrumMasterState.Playing)
        {
            _visualizer.Render(renderArgs);
            var context = renderArgs.Commands.WithCamera(_camera);
            foreach (var taikoLane in PlayerTaikoLanes) taikoLane.Render(context);
        }

        _sundexContext.UIContext.Render(renderArgs.Commands);
    }

    public override void TransitionedTo()
    {
        _visualizer.TextContainer.Greeting.Value = "DON'T LECTURE ME WITH YOUR THIRTY DOLLAR DRUM MASTER";
        Workflow.HandleAfterSequenceLoad = HandleAfterSequenceLoad;
    }

    private Task HandleAfterSequenceLoad(TimedEvents timedEvents, SequencePlayer sequencePlayer)
    {
        _visualizer.HandleAfterSequenceLoad(timedEvents, sequencePlayer);
        // TODO add handler for this class as well
        return Task.CompletedTask;
    }

    public override void Update(UpdateArguments updateArgs)
    {
        if (_state is DrumMasterState.Playing)
        {
            _visualizer.Update(updateArgs);
            foreach (var taikoLane in PlayerTaikoLanes) taikoLane.Update();
        }
        else
        {
            Workflow.Update();
        }

        _currentUI?.Update(_sundexContext.UIContext);
        _currentUI?.Layout();

        ProcessDeletionQueue();
    }

    private void QueueDeletion(UIElement element)
    {
        _deletionQueue.Enqueue(element);
    }

    private void ProcessDeletionQueue()
    {
        if (_deletionQueue.Count == 0) return;

        while (_deletionQueue.TryDequeue(out var element))
        {
            if (element is TaikoLaneConfiguration config) _taikoLaneConfigurations.Remove(config);

            if (element.Parent is Panel panel) panel.RemoveChild(element);
        }

        _currentUI?.Layout();
    }

    public override void Resize(int w, int h)
    {
        _camera.Viewport = (w, h);
        _camera.UpdateMatrix();

        _messageUI.InvalidateCoordinates();
        _configUI.InvalidateCoordinates();

        _messageUI.Layout();
        _configUI.Layout();

        _visualizer.Resize(w, h);
        foreach (var lane in PlayerTaikoLanes) lane.LaneScale = (w, 160);
    }

    public override void Shutdown()
    {
        foreach (var lane in PlayerTaikoLanes) lane.Dispose();

        PlayerTaikoLanes.Clear();
    }

    public override void FileDrop(string[] locations)
    {
        if (locations.Length == 1 && locations[0].EndsWith(".wav", StringComparison.Ordinal))
        {
            _visualizer.UpdateBackingTrack(locations[0]);
            return;
        }

        foreach (var configuration in _taikoLaneConfigurations) configuration.ReturnChildren();
        Workflow.SequencePlayer.GetTimingStopwatch().Reset();

        _visualizer.UpdateBackingTrack(null);
        var sequenceInfos = Workflow.GetSequenceInfos(locations);
        _pendingSequences = new Sequence[locations.Length];

        for (var index = 0; index < sequenceInfos.Length; index++)
        {
            var sequence_info = sequenceInfos[index];
            var asset = Runtime.AssetProvider.LoadString(sequence_info.FileLocation);
            var sequence = Sequence.FromString(asset);
            _pendingSequences[index] = sequence;
        }

        var usedSoundsSet = _pendingSequences.SelectMany(s => s.UsedSounds).Distinct().ToHashSet();
        var allAvailableSounds = Workflow.AtlasStore.StaticSounds.Keys
            .Concat(Workflow.AtlasStore.AnimatedSounds.Keys).Distinct();

        var filteredSounds = allAvailableSounds.Where(usedSoundsSet.Contains).ToList();

        // Reset UI components for configuration
        _soundList.Clear();
        foreach (var sound in filteredSounds) _soundList.AddSound(sound);
        _soundList.InvalidateLayout();

        foreach (var config in _taikoLaneConfigurations)
        {
            config.SelectedSounds.Clear();
            config.DropZone.Children.Clear();
            config.DropZone.InvalidateLayout();
        }

        ChangeState(DrumMasterState.Configuring);
    }

    private TaikoLane GenerateTaikoLaneFor(Placement[] placements, List<string> sounds, Vector2 position)
    {
        var soundMap = new TaikoSoundMap();
        foreach (var sound in sounds) soundMap.Bind(Keys.Unknown, sound);

        return new TaikoLane(placements, Workflow.SequencePlayer.GetTimingStopwatch(),
            soundMap, Workflow.AtlasStore, Visualizer.VisualizerFonts, _visualizer.PlayfieldSizing)
        {
            LanePosition = position,
            LaneScale = (Runtime.ClientSize.X, 160),
            AutoPlay = true
        };
    }

    public override void Keyboard(KeyboardInput state)
    {
        if (_state is DrumMasterState.Playing)
            if (state.IsKeyPressed(Keys.Escape))
            {
                ChangeState(DrumMasterState.Configuring);
                Workflow.SequencePlayer.GetTimingStopwatch().Reset();
                return;
            }

        _visualizer.Keyboard(state);
        foreach (var taikoLane in PlayerTaikoLanes) taikoLane.Keyboard(state);
    }

    public override void Mouse(PointerInput mouseState, KeyboardInput keyboardState)
    {
        _currentUI?.Test(mouseState, Vector2.One);
        if (mouseState.IsButtonReleased(MouseButton.Left)) DragManager.DraggedElement = null;
    }

    private enum DrumMasterState
    {
        WaitingForSequence,
        Configuring,
        Playing
    }
}
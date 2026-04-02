using DrumMasterScene.Components;
using DrumMasterScene.UI;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Audio;
using Sundex.Components.Abstractions;
using Sundex.Engine;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using Sundex.Markup;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using VisualizerScene;

namespace DrumMasterScene;

public class DrumMaster(Game game, ThirtyDollarWorkflow workflow) : Scene(game)
{
    private readonly Visualizer _visualizer = game.SceneManager.Get<Visualizer>();
    private readonly DollarStoreCamera _camera = new((0, 0, 0), (game.ClientSize.X, game.ClientSize.Y));
    private ThirtyDollarWorkflow Workflow { get; } = workflow;

    private SundexComponent _configUI = null!;
    private SundexComponent _messageUI = null!;
    private SundexContext _sundexContext = null!;

    private List<TaikoLane> PlayerTaikoLanes { get; set; } = [];

    private enum DrumMasterState
    {
        WaitingForSequence,
        Configuring,
        Playing
    }

    private readonly Queue<UIElement> _deletionQueue = [];

    private DrumMasterState _state = DrumMasterState.WaitingForSequence;
    private Sequence[] _pendingSequences = [];
    private readonly List<TaikoLaneConfiguration> _taikoLaneConfigurations = [];

    public override void Initialize(InitArguments initArguments)
    {
        var uiContext = new UIContext
        {
            Camera = _camera,
        };
        _sundexContext = new SundexContext(uiContext);

        // Register custom element factories
        _sundexContext.RegisterElementFactory("sound-list", ctx => new SoundList(ctx, Workflow.AtlasStore));
        _sundexContext.RegisterElementFactory("taiko-lane-configuration", ctx =>
        {
            var config = new TaikoLaneConfiguration(ctx, _sundexContext)
            {
                OnDeleteRequested = QueueDeletion
            };
            _taikoLaneConfigurations.Add(config);
            return config;
        });

        _configUI = _sundexContext.NewComponent(Game.AssetProvider.Load<StringAsset, StringInfo>(
            StringInfo.CreateFromUnknownStorage("UI/Select Sounds/SelectSounds.snx.xml")).Value);
        _configUI.RunLogic?.Invoke(this);
        _configUI.Element.Layout();

        _messageUI = _sundexContext.NewComponent(Game.AssetProvider.Load<StringAsset, StringInfo>(
            StringInfo.CreateFromUnknownStorage("UI/WaitingForSequence.snx.xml")).Value);
        _messageUI.RunLogic?.Invoke(this);
        _messageUI.Element.Layout();

        ChangeState(DrumMasterState.WaitingForSequence);
    }

    public void StartPlayback()
    {
        if (_pendingSequences.Length == 0) return;

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = 100_000,
            AddVisualEvents = false
        });

        var originalPlacements = calculator.CalculateMany(_pendingSequences).ToArray();
        foreach (var lane in PlayerTaikoLanes)
        {
            lane.Dispose();
        }

        PlayerTaikoLanes.Clear();
        var y = 100;
        foreach (var configuration in _taikoLaneConfigurations)
        {
            var sounds = configuration.SelectedSounds;
            PlayerTaikoLanes.Add(GenerateTaikoLaneFor(originalPlacements, sounds, (0, y)));
            y += 160 + 25;
        }

        ChangeState(DrumMasterState.Playing);

        Game.ThreadRunner.RunTask(Action);
        return;

        async void Action()
        {
            await Workflow.UpdateSequences(_pendingSequences);
        }
    }

    public override void Start()
    {
    }

    private SundexComponent? _currentUI;

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

        _currentUI?.Element.DrawTo(_sundexContext.UIContext);
    }

    public override void Render(RenderArguments renderArgs)
    {
        if (_state is DrumMasterState.Playing)
        {
            _visualizer.Render(renderArgs);
            foreach (var taikoLane in PlayerTaikoLanes)
            {
                taikoLane.Render(_camera);
            }
        }

        _sundexContext.UIContext.Render();
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
            foreach (var taikoLane in PlayerTaikoLanes)
            {
                taikoLane.Update();
            }
        }
        else Workflow.Update();

        _currentUI?.Element.Update(_sundexContext.UIContext);
        _currentUI?.Element.Layout();

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
            if (element is TaikoLaneConfiguration config)
            {
                _taikoLaneConfigurations.Remove(config);
            }

            if (element.Parent is Sundex.Components.Panels.Panel panel)
            {
                panel.RemoveChild(element);
            }
        }

        _currentUI?.Element.Layout();
    }

    public override void Resize(int w, int h)
    {
        _camera.Viewport = (w, h);
        _camera.UpdateMatrix();

        _messageUI.Element.InvalidateCoordinates();
        _configUI.Element.InvalidateCoordinates();

        _messageUI.Element.Layout();
        _configUI.Element.Layout();

        _visualizer.Resize(w, h);
        foreach (var lane in PlayerTaikoLanes)
        {
            lane.LaneScale = (w, 160);
        }
    }

    public override void Shutdown()
    {
        foreach (var lane in PlayerTaikoLanes)
        {
            lane.Dispose();
        }

        PlayerTaikoLanes.Clear();
    }

    public override void FileDrop(string[] locations)
    {
        if (locations.Length == 1 && locations[0].EndsWith(".wav"))
        {
            _visualizer.UpdateBackingTrack(locations[0]);
            return;
        }

        foreach (var configuration in _taikoLaneConfigurations)
        {
            configuration.ReturnChildren();
        }
        Workflow.SequencePlayer.GetTimingStopwatch().Reset();

        _visualizer.UpdateBackingTrack(null);
        var sequenceInfos = Workflow.GetSequenceInfos(locations);
        _pendingSequences = new Sequence[locations.Length];

        for (var index = 0; index < sequenceInfos.Length; index++)
        {
            var sequence_info = sequenceInfos[index];
            var asset = Game.AssetProvider.Load<StringAsset, StringInfo>(
                StringInfo.CreateFromUnknownStorage(sequence_info.FileLocation));
            var sequence = Sequence.FromString(asset.Value);
            _pendingSequences[index] = sequence;
        }

        var usedSoundsSet = _pendingSequences.SelectMany(s => s.UsedSounds).Distinct().ToHashSet();
        var allAvailableSounds = Workflow.AtlasStore.StaticSounds.Keys
            .Concat(Workflow.AtlasStore.AnimatedSounds.Keys).Distinct();

        var filteredSounds = allAvailableSounds.Where(usedSoundsSet.Contains).ToList();

        // Reset UI components for configuration
        if (_configUI.RegisteredIDs.TryGetValue("available-sounds", out var soundListElement) &&
            soundListElement is SoundList soundList)
        {
            soundList.Clear();
            foreach (var sound in filteredSounds)
            {
                soundList.AddSound(sound);
            }
            soundList.InvalidateLayout();
        }

        // Find all taiko-lane-configuration elements and clear them
        foreach (var registeredElement in _configUI.RegisteredIDs.Values)
        {
            if (registeredElement is not TaikoLaneConfiguration config) continue;
            config.SelectedSounds.Clear();
            config.DropZone.Children.Clear();
            config.DropZone.InvalidateLayout();
        }

        ChangeState(DrumMasterState.Configuring);
    }

    private TaikoLane GenerateTaikoLaneFor(Placement[] placements, List<string> sounds, Vector2 position)
    {
        var soundMap = new TaikoSoundMap();
        foreach (var sound in sounds)
        {
            soundMap.Bind(Keys.Unknown, sound);
        }

        return new TaikoLane(placements, Workflow.SequencePlayer.GetTimingStopwatch(),
            soundMap, Workflow.AtlasStore, Visualizer.VisualizerFonts, _visualizer.PlayfieldSizing)
        {
            LanePosition = position,
            LaneScale = (Game.ClientSize.X, 160),
            AutoPlay = true
        };
    }

    public override void Keyboard(KeyboardState state)
    {
        if (_state is DrumMasterState.Playing)
        {
            if (state.IsKeyPressed(Keys.Escape))
            {
                ChangeState(DrumMasterState.Configuring);
                Workflow.SequencePlayer.GetTimingStopwatch().Reset();
                return;
            }
        }

        _visualizer.Keyboard(state);
        foreach (var taikoLane in PlayerTaikoLanes)
        {
            taikoLane.Keyboard(state);
        }
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        _currentUI?.Element.Test(mouseState, Vector2.One);
        if (mouseState.IsButtonReleased(MouseButton.Left))
        {
            DragManager.DraggedElement = null;
        }
    }
}
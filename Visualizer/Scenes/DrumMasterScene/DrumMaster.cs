using DrumMasterScene.Components;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Audio;
using Sundex.Engine;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using VisualizerScene;

namespace DrumMasterScene;

public class DrumMaster(Game game, ThirtyDollarWorkflow workflow) : Scene(game)
{
    private const string TaikoDon = "taiko_don";
    private const string TaikoKa = "taiko_ka";
    
    private static readonly (string, Keys)[] OtherDrumSounds =
    [
        (TaikoDon, Keys.Unknown),
        ("adofaikick", Keys.Unknown), ("🥁", Keys.Unknown), ("hammer", Keys.Unknown), ("🪘", Keys.Unknown),
        ("🏏", Keys.Unknown), ("adofai_fire", Keys.Unknown), ("tab_rows", Keys.Unknown), ("midspin", Keys.Unknown),
        ("noteblock_snare", Keys.Unknown),
    ];

    private static readonly (string, Keys)[] OtherCymbalSounds =
    [
        (TaikoKa, Keys.Unknown),
        ("adofaicymbal", Keys.Unknown), ("shaker", Keys.Unknown), ("ride2", Keys.Unknown), ("hitmarker", Keys.Unknown),
        ("adofai_ice", Keys.Unknown), ("rdclap", Keys.Unknown), ("tab_rooms", Keys.Unknown),
        ("noteblock_click", Keys.Unknown), ("whipcrack", Keys.Unknown), ("sidestick", Keys.Unknown),
        ("pan", Keys.Unknown)
    ];

    private readonly Visualizer _visualizer = game.SceneManager.Get<Visualizer>();
    private readonly DollarStoreCamera _camera = new((0, 0, 0), (game.ClientSize.X, game.ClientSize.Y));

    private ThirtyDollarWorkflow Workflow { get; } = workflow;

    private List<TaikoLane> TaikoLanes { get; set; } = [];

    public override void Initialize(InitArguments initArguments)
    {
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments renderArgs)
    {
        _visualizer.Render(renderArgs);
        foreach (var taikoLane in TaikoLanes)
        {
            taikoLane.Render(_camera);
        }
    }

    public override void TransitionedTo()
    {
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
        _visualizer.Update(updateArgs);
        foreach (var taikoLane in TaikoLanes)
        {
            taikoLane.Update();
        }
    }

    public override void Resize(int w, int h)
    {
        _camera.Viewport = (w, h);
        _camera.UpdateMatrix();

        _visualizer.Resize(w, h);
        foreach (var lane in TaikoLanes)
        {
            lane.LaneScale = (w, 200);
        }
    }

    public override void Shutdown()
    {
        _visualizer.Shutdown();
        foreach (var lane in TaikoLanes)
        {
            lane.Dispose();
        }

        TaikoLanes.Clear();
    }

    public override void FileDrop(string[] locations)
    {
        var sequenceInfos = Workflow.GetSequenceInfos(locations);
        var sequences = new Sequence[locations.Length];

        for (var index = 0; index < sequenceInfos.Length; index++)
        {
            var sequence_info = sequenceInfos[index];
            var asset = Game.AssetProvider.Load<StringAsset, StringInfo>(
                StringInfo.CreateFromUnknownStorage(sequence_info.FileLocation));
            var sequence = Sequence.FromString(asset.Value);
            sequences[index] = sequence;
        }

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = 100_000,
            AddVisualEvents = false
        });


        var originalPlacements = calculator.CalculateMany(sequences).ToArray();
        foreach (var lane in TaikoLanes)
        {
            lane.Dispose();
        }

        TaikoLanes.Clear();
        TaikoLanes.Add(GenerateTaikoLaneFor(originalPlacements, OtherDrumSounds, (0, 100)));
        TaikoLanes.Add(GenerateTaikoLaneFor(originalPlacements, OtherCymbalSounds, (0, 325)));

        Game.ThreadRunner.RunTask(Action);
        return;

        async void Action()
        {
            await Workflow.UpdateSequences(sequences);
        }
    }

    private TaikoLane GenerateTaikoLaneFor(Placement[] placements, (string, Keys)[] sounds, Vector2 position)
    {
        var soundMap = new TaikoSoundMap();
        foreach (var (sound, key) in sounds)
        {
            soundMap.Bind(key, sound);
        }

        return new TaikoLane(placements, Workflow.SequencePlayer.GetTimingStopwatch(),
            soundMap, Workflow.AtlasStore, Visualizer.VisualizerFonts, _visualizer.PlayfieldSizing)
        {
            LanePosition = position,
            LaneScale = (Game.ClientSize.X, 200),
            AutoPlay = true
        };
    }

    public override void Keyboard(KeyboardState state)
    {
        _visualizer.Keyboard(state);
        foreach (var taikoLane in TaikoLanes)
        {
            taikoLane.Keyboard(state);
        }
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
    }
}
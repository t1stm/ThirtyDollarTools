using LoadingScene.Reports;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Audio;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Scenes;
using ThirtyDollarVisualizer.Engine.Scenes.Arguments;

namespace LoadingScene;

[PreloadGraphicsContext]
public class Loader : Scene, IGamePreloadable
{
    private static AssetProvider _assetProvider = null!;
    private readonly AudioContext? _context;

    private readonly ThirtyDollarDownloader _thirtyDollarDownloader;
    private IProgressReport _progressReport = new NotStartedReport();

    public Action<ThirtyDollarWorkflow>? OnFinish;

    public Loader(SceneManager sceneManager, AudioContext? context) : base(sceneManager)
    {
        _context = context;
        _thirtyDollarDownloader = new ThirtyDollarDownloader(sceneManager.Game.ThreadRunner, _assetProvider)
        {
            StatusUpdate = StatusUpdate
        };
    }

    public bool Finished { get; private set; }

    public static void Preload(AssetProvider assetProvider)
    {
        _assetProvider = assetProvider;
    }

    private void StatusUpdate(IProgressReport obj)
    {
        lock (_progressReport)
        {
            _progressReport = obj;
        }
    }

    public override void Initialize(InitArguments initArguments)
    {
        // maybe?
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments renderArgs)
    {
    }

    public override void TransitionedTo()
    {
        _thirtyDollarDownloader.Load();
        // Trigger Animations
    }

    public override void Update(UpdateArguments updateArgs)
    {
        // TODO: progress bar update here.
        if (!_thirtyDollarDownloader.AssetsLoaded && !Finished) return;

        var workflow = new ThirtyDollarWorkflow(Game, Logger, _context)
        {
            AtlasStore = _thirtyDollarDownloader.AtlasStore,
            SampleHolder = _thirtyDollarDownloader.SampleHolder
        };
        OnFinish?.Invoke(workflow);
        Finished = true;
    }

    public override void Resize(int w, int h)
    {
    }

    public override void Shutdown()
    {
    }

    public override void FileDrop(string[] locations)
    {
    }

    public override void Keyboard(KeyboardState state)
    {
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
    }
}
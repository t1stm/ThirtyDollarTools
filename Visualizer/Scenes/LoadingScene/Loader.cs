using Components.Abstractions;
using LoadingScene.Reports;
using LoadingScene.Scene;
using OpenTK.Mathematics;
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
public class Loader : ThirtyDollarVisualizer.Engine.Scenes.Scene, IGamePreloadable
{
    private static AssetProvider _assetProvider = null!;
    private readonly AudioContext? _audioContext;
    private readonly UIContext _context;
    private readonly DollarStoreCamera _camera;

    private readonly ThirtyDollarDownloader _thirtyDollarDownloader;
    private IProgressReport _progressReport = new NotStartedReport();
    
    public Action<ThirtyDollarWorkflow>? OnFinish { get; set; }
    public bool Finished { get; private set; }
    
    private readonly LoaderInterface _loaderInterface;

    public Loader(SceneManager sceneManager, AudioContext? audioContext) : base(sceneManager)
    {
        _audioContext = audioContext;
        _camera = new DollarStoreCamera(Vector3.Zero, sceneManager.Game.ClientSize);
        _context = new UIContext
        {
            Camera = _camera
        };
        _thirtyDollarDownloader = new ThirtyDollarDownloader(sceneManager.Game.ThreadRunner, _assetProvider)
        {
            StatusUpdate = StatusUpdate
        };
        
        _loaderInterface = new LoaderInterface(_context, _camera, () => _thirtyDollarDownloader.Load());
    }

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
        _loaderInterface.Render(_camera, _context);
    }

    public override void TransitionedTo()
    {
        
    }

    public override void Update(UpdateArguments updateArgs)
    {
        lock (_progressReport)
        {
            var progressReport = _progressReport;
            _loaderInterface.Update(progressReport, Game.MouseState);
        }
        
        if (!_thirtyDollarDownloader.AssetsLoaded && !Finished) return;

        var workflow = new ThirtyDollarWorkflow(Game, Logger, _audioContext)
        {
            AtlasStore = _thirtyDollarDownloader.AtlasStore,
            SampleHolder = _thirtyDollarDownloader.SampleHolder
        };
        OnFinish?.Invoke(workflow);
        Finished = true;
    }

    public override void Resize(int w, int h)
    {
        _camera.Viewport = new Vector2i(w, h);
        _camera.UpdateMatrix();
        _loaderInterface.Resize(w, h);
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
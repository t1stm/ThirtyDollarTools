using Sundex.Components.Abstractions;
using LoadingScene.Reports;
using LoadingScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Audio;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;

namespace LoadingScene;

[PreloadGraphicsContext]
public class Loader : Scene, IGamePreloadable
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
    private Vector2 _lastScale = Vector2.One;
    private CursorType _cursorType = CursorType.Normal;

    public Loader(SceneManager sceneManager, AudioContext? audioContext) : base(sceneManager)
    {
        var clientSize = sceneManager.Game.ClientSize;
        if (sceneManager.Game.TryGetCurrentMonitorScale(out var scaleX, out var scaleY))
        {
            _lastScale = new Vector2(scaleX, scaleY);
            clientSize.X = (int)(clientSize.X / scaleX);
            clientSize.Y = (int)(clientSize.Y / scaleY);
        }
        
        _audioContext = audioContext;
        _camera = new DollarStoreCamera(Vector3.Zero, clientSize);
        
        _context = new UIContext
        {
            Camera = _camera,
            RequestCursor = type => _cursorType = type
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
        _loaderInterface.StartAnimations();
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _cursorType = CursorType.Normal;
        var mouseState = Game.MouseState;
        
        lock (_progressReport)
        {
            var progressReport = _progressReport;
            _loaderInterface.Update(progressReport, _context, mouseState, _lastScale);
        }
        
        Game.Cursor = _cursorType switch
        {
            CursorType.Normal => MouseCursor.Default,
            CursorType.Pointer => MouseCursor.PointingHand,
            CursorType.ResizeX => MouseCursor.ResizeEW,
            CursorType.ResizeY => MouseCursor.ResizeNS,
            _ => MouseCursor.Default
        };
        
        if (!_thirtyDollarDownloader.AssetsLoaded && !Finished) return;
        Finished = true;
        
        var workflow = new ThirtyDollarWorkflow(Game, Logger, _audioContext)
        {
            AtlasStore = _thirtyDollarDownloader.AtlasStore,
            SampleHolder = _thirtyDollarDownloader.SampleHolder
        };
        OnFinish?.Invoke(workflow);
    }

    public override void Resize(int w, int h)
    {
        float width = w;
        float height = h;
        if (Game.TryGetCurrentMonitorScale(out var scaleX, out var scaleY))
        {
            width /= scaleX;
            height /= scaleY;
            _lastScale = new Vector2(scaleX, scaleY);
        }
        
        _camera.Viewport = new Vector2i((int)width, (int)height);
        _camera.UpdateMatrix();
        
        _loaderInterface.Resize(_camera.Width, _camera.Height);
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
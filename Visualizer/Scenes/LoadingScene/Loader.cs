using HomeScene;
using LoadingScene.Background;
using LoadingScene.Reports;
using LoadingScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Audio;
using Sundex.Components.Abstractions;
using Sundex.Engine;
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
    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;

    private readonly LoaderInterface _loaderInterface;

    private readonly ThirtyDollarDownloader _thirtyDollarDownloader;
    private CursorType _cursorType = CursorType.Normal;
    private Vector2 _lastScale = Vector2.One;
    private IProgressReport _progressReport = new NotStartedReport();
    private readonly DollarStoreLoaderBackground _background;

    public Loader(Game game, AudioContext? audioContext) : base(game)
    {
        var clientSize = game.ClientSize;
        if (game.TryGetScreenScale(out var scaleX, out var scaleY))
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
        _thirtyDollarDownloader = new ThirtyDollarDownloader(game.ThreadRunner, _assetProvider)
        {
            StatusUpdate = StatusUpdate
        };
        
        _background = new DollarStoreLoaderBackground(game.AssetProvider.DeleteQueue)
        {
            AtlasStore = _thirtyDollarDownloader.AtlasStore
        };
        _thirtyDollarDownloader.OnLoadSound = sound => _background.AddSound(sound);
        
        _loaderInterface = new LoaderInterface(_context, () => _thirtyDollarDownloader.Load());
    }

    public Action<ThirtyDollarWorkflow>? OnFinish { get; set; }
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
        _background.Render();
        _context.Render();
    }

    public override void TransitionedTo()
    {
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _background.Update();
        _cursorType = CursorType.Normal;

        lock (_progressReport)
        {
            var progressReport = _progressReport;
            _loaderInterface.Update(progressReport, _context);
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
        _loaderInterface.Label.SetTextContents("Loading interface...");
        Finished = true;

        var workflow = new ThirtyDollarWorkflow(Game, Logger, _thirtyDollarDownloader.SampleHolder, _thirtyDollarDownloader.AtlasStore, _audioContext);
        OnFinish?.Invoke(workflow);
    }

    public override void Resize(int w, int h)
    {
        float width = w;
        float height = h;
        if (Game.TryGetScreenScale(out var scaleX, out var scaleY))
        {
            width /= scaleX;
            height /= scaleY;
            _lastScale = new Vector2(scaleX, scaleY);
        }

        _camera.Viewport = new Vector2i((int)width, (int)height);
        _camera.UpdateMatrix();

        _background.Resize(_camera.Viewport.X, _camera.Viewport.Y);
        _loaderInterface.Resize();
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
        _loaderInterface.MouseEvent(mouseState, _lastScale);
    }
}
using LoadingScene.Background;
using LoadingScene.Reports;
using LoadingScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Audio;
using Shared.Updates;
using Sundex.Components.Abstractions;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using VisualizerScene.Settings;

namespace LoadingScene;

[PreloadGraphicsContext]
public class Loader : Scene, IGamePreloadable
{
    private static AssetProvider _assetProvider = null!;
    private readonly AudioContext? _audioContext;
    private readonly DollarStoreLoaderBackground _background;
    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;

    private readonly LoaderInterface _loaderInterface;
    private readonly ThirtyDollarDownloader _thirtyDollarDownloader;
    private readonly VisualizerSettings _settings;
    private readonly VersionInfo? _version;
    private bool _autoLoadStarted;
    private CursorType _cursorType = CursorType.Default;
    private Vector2 _lastScale = Vector2.One;
    private IProgressReport _progressReport = new NotStartedReport();

    /// <summary>True once the update prompt is out of the way - nothing loads before then.</summary>
    private bool _updatePromptAnswered;

    public Loader(Game game, AudioContext? audioContext, VisualizerSettings settings, VersionInfo? version) : base(game)
    {
        _settings = settings;
        _version = version;

        var clientSize = game.ClientSize;
        if (game.TryGetScreenScale(out var scaleX, out var scaleY))
            _lastScale = new Vector2(scaleX, scaleY);

        _audioContext = audioContext;
        _camera = new DollarStoreCamera(Vector3.Zero, clientSize);

        _context = new UIContext
        {
            Camera = _camera,
            PixelScale = _lastScale,
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
        SetUpUpdatePrompt();
    }

    /// <summary>
    ///     Shows the opt-in question on the first run, and otherwise starts the check the user
    ///     already agreed to. A build with no VERSION date (a developer build) is never asked:
    ///     there is no build date to call a release newer than, so the answer couldn't be used.
    /// </summary>
    private void SetUpUpdatePrompt()
    {
        _updatePromptAnswered = _settings.UpdateCheckAsked || _version?.Date is null;
        if (_updatePromptAnswered)
        {
            _loaderInterface.UpdatePrompt.Visible = false;
            if (_settings.CheckForUpdates)
                UpdateChecker.Start(_version, _settings.UpdateIncludePrereleases,
                    _settings.UpdateIncludeNightlies, Logger);
            return;
        }

        _loaderInterface.MainView.Visible = false;

        // Default to the channel this build came from: a prerelease ticks prereleases, and a
        // nightly ticks both (the nightly workflow marks its releases prerelease as well).
        _loaderInterface.IncludePrereleases.Checked = _version?.Prerelease ?? false;
        _loaderInterface.IncludeNightlies.Checked = _version?.Nightly ?? false;

        _loaderInterface.UpdateDecline.OnClick = _ => AnswerUpdatePrompt(false);
        _loaderInterface.UpdateOptIn.OnClick = _ => AnswerUpdatePrompt(true);
    }

    private void AnswerUpdatePrompt(bool optIn)
    {
        _settings.UpdateIncludePrereleases = _loaderInterface.IncludePrereleases.Checked;
        _settings.UpdateIncludeNightlies = _loaderInterface.IncludeNightlies.Checked;
        _settings.CheckForUpdates = optIn;
        // Last, because every setter writes the settings file: this one is what stops the
        // question from being asked again.
        _settings.UpdateCheckAsked = true;

        _loaderInterface.UpdatePrompt.Visible = false;
        _loaderInterface.MainView.Visible = true;
        _updatePromptAnswered = true;

        if (optIn)
            UpdateChecker.Start(_version, _settings.UpdateIncludePrereleases,
                _settings.UpdateIncludeNightlies, Logger);
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
        _cursorType = CursorType.Default;

        // Nothing loads while the update prompt is up - the sounds are usually already
        // there, and the automatic load below would otherwise run straight past the question.
        if (_updatePromptAnswered && !_autoLoadStarted)
        {
            _autoLoadStarted = true;
            Task.Run(async () =>
            {
                StatusUpdate(new SampleLoadingReport
                {
                    Message = "Checking for new sounds...",
                    Percentage = 0
                });
                if (!await _thirtyDollarDownloader.IsDownloadNecessary()) _thirtyDollarDownloader.Load();
            });
        }

        lock (_progressReport)
        {
            var progressReport = _progressReport;
            _loaderInterface.Update(progressReport, _context);
        }

        var cursor = _cursorType switch
        {
            CursorType.Default => MouseCursor.Default,
            CursorType.Pointer => MouseCursor.PointingHand,
            CursorType.Text => MouseCursor.IBeam,
            CursorType.ResizeX => MouseCursor.ResizeEW,
            CursorType.ResizeY => MouseCursor.ResizeNS,
            _ => MouseCursor.Default
        };

        if (Game.Cursor != cursor)
            Game.Cursor = cursor;

        if (!_thirtyDollarDownloader.AssetsLoaded && !Finished) return;
        _loaderInterface.Label.SetTextContents("Loading interface...");
        Finished = true;

        var workflow = new ThirtyDollarWorkflow(Game, Logger, _thirtyDollarDownloader.SampleHolder,
            _thirtyDollarDownloader.AtlasStore, _audioContext);
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
        _context.PixelScale = _lastScale;

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
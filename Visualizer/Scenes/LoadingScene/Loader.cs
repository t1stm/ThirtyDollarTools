using System.Diagnostics;
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
using Sundex.Markup;
using VisualizerScene;
using VisualizerScene.Settings;

namespace LoadingScene;

[PreloadGraphicsContext]
public class Loader : Scene, IGamePreloadable
{
    /// <summary>
    ///     How long the loader's exit takes, in seconds. HomeInterface.SweepSeconds and
    ///     LoaderInterface.ExitSeconds are timed against the same window and measured from
    ///     <see cref="ExitFadeStart" />, so the three move together.
    /// </summary>
    private const float ExitSeconds = 1.5f;

    /// <summary>
    ///     Printable ASCII - the character set the interface screens are warmed with.
    ///     Anything outside it is generated on demand on the frame that first draws it.
    /// </summary>
    private const string WarmCharacters =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    /// <summary>
    ///     Seconds into the exit at which the target scene starts fading up. The sound field
    ///     drains from the first frame, so it moves before anything is drawn over it.
    /// </summary>
    private const float ExitFadeStart = 0.5f;

    private static AssetProvider _assetProvider = null!;
    private readonly AudioContext? _audioContext;
    private readonly DollarStoreLoaderBackground _background;
    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;

    private readonly LoaderInterface _loaderInterface;

    /// <summary>True on a first run: the setup screen is shown and decides when loading starts.</summary>
    private readonly bool _setupNeeded;

    private readonly ThirtyDollarDownloader _thirtyDollarDownloader;
    private readonly VisualizerSettings _settings;
    private readonly VersionInfo? _version;
    private readonly ThirtyDollarWorkflow _workflow;

    /// <summary>True once the boot work has been kicked off - see <see cref="Update" />.</summary>
    private bool _bootStarted;

    /// <summary>How many of <see cref="Preloads" /> have been built.</summary>
    private int _preloadsDone;

    private bool _preloadsQueued;

    /// <summary>
    ///     The warm-up tasks the scene builds depend on. Both must have completed before a
    ///     scene is built - see <see cref="QueuePreloadsWhenWarm" />.
    /// </summary>
    private Task? _componentWarm;

    private Task? _fontWarm;

    /// <summary>
    ///     Whether the user has agreed to the download. True from the start on every boot
    ///     past the first; on a first run the setup's last button sets it.
    /// </summary>
    private bool _startRequested;

    private bool _downloadStarted;

    private readonly Stopwatch _exitClock = new();
    private Scene? _target;
    private bool _exitStarted;
    private CursorType _cursorType = CursorType.Default;
    private Vector2 _lastScale = Vector2.One;
    private IProgressReport _progressReport = new NotStartedReport();

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

        // The sample holder and atlas store exist from the downloader's construction and
        // fill in as files arrive, so tool scenes can be handed this and built before the
        // download has produced anything.
        _workflow = new ThirtyDollarWorkflow(game, Logger, _thirtyDollarDownloader.SampleHolder,
            _thirtyDollarDownloader.AtlasStore, _audioContext);

        // The resampler is stored as a name plus its parameters, so it is rebuilt whenever
        // one of them changes. Filtered by name because building one is expensive and most
        // settings have nothing to do with it.
        _workflow.EncoderSettings.Resampler = Resamplers.Create(settings);
        settings.Changed += name =>
        {
            if (Resamplers.Properties.Contains(name))
                _workflow.EncoderSettings.Resampler = Resamplers.Create(settings);
        };

        _background = new DollarStoreLoaderBackground(game.AssetProvider.DeleteQueue)
        {
            AtlasStore = _thirtyDollarDownloader.AtlasStore
        };
        _thirtyDollarDownloader.OnLoadSound = sound => _background.AddSound(sound);

        _loaderInterface = new LoaderInterface(_context)
        {
            OnUpdateAnswer = AnswerUpdatePrompt,
            OnStartLoading = StartLoading
        };

        // Started before either path below returns, so the warm-ups overlap the download on
        // a returning boot and the setup being read on a first run.
        WarmComponents();
        WarmFonts();

        _setupNeeded = !_settings.UpdateCheckAsked;
        if (!_setupNeeded)
        {
            if (_settings.CheckForUpdates)
                UpdateChecker.Start(_version, _settings.UpdateIncludePrereleases,
                    _settings.UpdateIncludeNightlies, Logger);

            // Permission was given once and the download skips whatever is already on disk,
            // so a returning boot only waits for the scenes to be up.
            _startRequested = true;
            _loaderInterface.BeginLoading();
            return;
        }

        // Default to the channel this build came from: a prerelease ticks prereleases, and a
        // nightly ticks both (the nightly workflow marks its releases prerelease as well).
        _loaderInterface.IncludePrereleases.Checked = _version?.Prerelease ?? false;
        _loaderInterface.IncludeNightlies.Checked = _version?.Nightly ?? false;

        // A build with no VERSION date (a developer build) still gets the setup, minus the
        // update question: there is no build date to compare a release against.
        _loaderInterface.BeginSetup(_version?.Date is not null);
    }

    /// <summary>
    ///     Compiles every screen's logic block on a worker, before any of them is built, so
    ///     the Roslyn compilation that dominates a component build is not paid on the render
    ///     thread. Runs in its own task alongside <see cref="WarmFonts" />; the scene builds
    ///     wait for both - see <see cref="QueuePreloadsWhenWarm" />.
    /// </summary>
    private Task WarmComponents()
    {
        return ThreadRunner.RunTask(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            // A throwaway context: precompiling only touches the markup parser, the asset
            // provider and the script cache, none of which is per-context state.
            var compiled = new SundexContext(_context).PrecompileLogic();

            Logger.Debug("[Component Warmup] Compiled {Count} logic blocks in {Elapsed} ms",
                compiled, stopwatch.ElapsedMilliseconds);
        });
    }

    /// <summary>
    ///     Generates the MSDF for every glyph in <see cref="WarmCharacters" /> on a worker,
    ///     before any screen is built. Drawing a character the first time is MSDF generation
    ///     plus an atlas upload, and only the upload needs a graphics context, so the scene
    ///     builds that follow are left with just the uploads.
    /// </summary>
    private Task WarmFonts()
    {
        return ThreadRunner.RunTask(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Three atlases with separate glyph caches: the interface's own font, and
                // the two the visualizer's text container and player bar draw with. Twemoji
                // is skipped - its glyph set is too large to warm and no label uses it.
                _context.TextProvider.Warm(WarmCharacters);
                Visualizer.VisualizerFonts.LatoRegularProvider.Warm(WarmCharacters);
                Visualizer.VisualizerFonts.LatoBoldProvider.Warm(WarmCharacters);
            }
            catch (Exception e)
            {
                // Warming is an optimisation: a failure must not fail the boot, since any
                // glyph it missed is generated on demand.
                Logger.Debug("[Font Warmup] Failed with error: '{Exception}'", e);
                return;
            }

            Logger.Debug("[Font Warmup] Generated {Count} glyphs per atlas in {Elapsed} ms",
                WarmCharacters.Length, stopwatch.ElapsedMilliseconds);
        });
    }

    /// <summary>
    ///     Records the user's agreement to the download at the end of the setup, and marks
    ///     the setup done. The download itself waits for the scenes to be up - see
    ///     <see cref="StartDownloadWhenReady" />. The setup is marked done here rather than
    ///     in <see cref="AnswerUpdatePrompt" />, which a build with no date never reaches.
    /// </summary>
    private void StartLoading()
    {
        _settings.UpdateCheckAsked = true;
        _startRequested = true;
    }

    private void AnswerUpdatePrompt(bool optIn)
    {
        _settings.UpdateIncludePrereleases = _loaderInterface.IncludePrereleases.Checked;
        _settings.UpdateIncludeNightlies = _loaderInterface.IncludeNightlies.Checked;
        _settings.CheckForUpdates = optIn;
        // UpdateCheckAsked is owned by StartLoading, so the setup only counts as done once
        // it has been finished.

        if (optIn)
            UpdateChecker.Start(_version, _settings.UpdateIncludePrereleases,
                _settings.UpdateIncludeNightlies, Logger);
    }

    /// <summary>
    ///     The scenes to build before the sounds start coming down, in the order they are
    ///     built. Home belongs first: the exit hands over to it.
    /// </summary>
    public required IReadOnlyList<ScenePreload> Preloads { get; init; }

    /// <summary>
    ///     The scene the boot hands off to, from <c>--mode</c>. "home" unless told
    ///     otherwise, and reset to it when the name names nothing - see
    ///     <see cref="UpdateExit" />.
    /// </summary>
    public string ExitTo { get; set; } = "home";

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

        // Started here rather than in the constructor so the first frame is on screen
        // before the disk, the network and the render thread all get busy at once.
        if (!_bootStarted)
        {
            _bootStarted = true;

            // sounds.json is requested now so it is in hand by the time the download it
            // feeds is allowed to start.
            _thirtyDollarDownloader.LoadSampleList();

            _componentWarm = WarmComponents();
            _fontWarm = WarmFonts();

            StatusUpdate(new ScenePreloadReport
            {
                Message = "Preparing the interface",
                Detail = "compiling layouts"
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

        QueuePreloadsWhenWarm();
        StartDownloadWhenReady();

        if (!_thirtyDollarDownloader.AssetsLoaded) return;

        // Guarded on Finished so the message is set once, not on every frame between the
        // assets landing and the transition taking effect.
        if (!Finished)
        {
            _loaderInterface.StatusMessage.SetTextContents("Opening the Visualizer");
            Finished = true;
            return;
        }

        UpdateExit();
    }

    /// <summary>
    ///     Holds the scene builds until both warm-ups have landed, so a build never blocks
    ///     on the work meant to make it cheap. The render thread animates this screen while
    ///     the workers run, which it cannot do while a scene is being built.
    /// </summary>
    private void QueuePreloadsWhenWarm()
    {
        if (_preloadsQueued) return;
        if (_componentWarm is not { IsCompleted: true }) return;
        if (_fontWarm is not { IsCompleted: true }) return;

        _preloadsQueued = true;
        QueuePreloads();
    }

    /// <summary>
    ///     Queues every scene in <see cref="Preloads" /> to be built, a frame apart. Two
    ///     enqueued events per scene, because <see cref="Sundex.Engine.Game" /> runs one per
    ///     frame: the status message gets a frame of its own and is on screen before the
    ///     build it describes takes the next.
    /// </summary>
    private void QueuePreloads()
    {
        for (var index = 0; index < Preloads.Count; index++)
        {
            var preload = Preloads[index];
            var step = index;

            Game.Enqueue(_ => StatusUpdate(new ScenePreloadReport
            {
                Message = preload.Message,
                Detail = $"{step + 1} of {Preloads.Count}",
                Percentage = (step + 1) / (double)Preloads.Count
            }));

            Game.Enqueue(game =>
            {
                preload.Load(game, _workflow);
                // Render thread, same as Update - no interlock needed.
                _preloadsDone++;
            });
        }
    }

    /// <summary>
    ///     Starts the sound download once the user has agreed to it, every scene is built
    ///     and sounds.json is in. The scene builds go first because they are render-thread
    ///     work and would otherwise stutter the progress this screen is drawing.
    /// </summary>
    private void StartDownloadWhenReady()
    {
        if (_downloadStarted || !_startRequested) return;
        if (_preloadsDone < Preloads.Count) return;

        if (!_thirtyDollarDownloader.SampleListLoaded)
        {
            // The scenes are up but sounds.json is still in flight; say what is being
            // waited on rather than leave the last scene's message up.
            StatusUpdate(new LoadingSoundsListReport());
            return;
        }

        _downloadStarted = true;
        Logger.Debug("[Boot] {Count} scenes preloaded and the sound list is in - starting the download",
            Preloads.Count);
        _thirtyDollarDownloader.Load();
    }

    /// <summary>
    ///     Runs the hand-off to <see cref="ExitTo" /> - the home screen unless --mode named
    ///     another. Holds on the loading screen until that scene exists, since Program loads
    ///     the scenes a frame apart.
    ///     <br /><br />
    ///     The exit itself: the sound field stops bouncing and scatters off the top edge, then
    ///     the strip settles back to the zero height it rose from while the scene underneath
    ///     fades up through it. Home's playhead starts the moment it is transitioned to,
    ///     continuing the left-to-right motion across the seam.
    /// </summary>
    private void UpdateExit()
    {
        if (_target is null)
        {
            // Every scene is built by the time the exit runs, so a miss is a bad --mode
            // rather than a build still in flight; fall back instead of hanging here.
            if (!SceneManager.Scenes.TryGetValue(ExitTo, out var scene))
            {
                Logger.Warning("[Boot] No scene named \"{Mode}\" - opening the home screen instead", ExitTo);
                ExitTo = "home";
                return;
            }

            _target = scene;
            SetTargetAlpha(0f);
            _exitClock.Restart();
        }

        var elapsed = (float)_exitClock.Elapsed.TotalSeconds;
        _background.Drain(Math.Clamp(elapsed / ExitSeconds, 0f, 1f));
        if (elapsed < ExitFadeStart) return;

        if (!_exitStarted)
        {
            _exitStarted = true;
            _loaderInterface.BeginExit();
            // Both scenes render for the rest of the exit, this one underneath: the
            // target's stage is opaque, so it covers the loader on its own as it comes up.
            SceneManager.TransitionTo([this, _target]);
        }

        var fade = Math.Clamp((elapsed - ExitFadeStart) / (ExitSeconds - ExitFadeStart), 0f, 1f);
        SetTargetAlpha(fade * fade * (3f - 2f * fade));

        if (fade < 1f) return;
        _exitClock.Stop();
        SceneManager.TransitionTo(ExitTo);
    }

    /// <summary>
    ///     Fades the scene being handed off to, when it implements <see cref="IFadeInScene" />.
    ///     A scene that does not still gets the rest of the exit, but arrives opaque.
    /// </summary>
    private void SetTargetAlpha(float alpha)
    {
        if (_target is IFadeInScene target) target.InterfaceAlpha = alpha;
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
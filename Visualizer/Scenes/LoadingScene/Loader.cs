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
    ///     How long this screen takes to get off, which is not how long the hand-off takes:
    ///     the home screen's playhead is still sweeping when the loader is dropped, and that
    ///     sweep landing is the last beat of the transition, at 2.5s. See
    ///     HomeInterface.SweepSeconds, which is measured from <see cref="ExitFadeStart" />.
    ///     <br /><br />
    ///     The strip's own fade (LoaderInterface.ExitSeconds) covers everything after
    ///     <see cref="ExitFadeStart" />, so the two move together.
    /// </summary>
    private const float ExitSeconds = 1.5f;

    /// <summary>
    ///     Every character the interface screens spell their labels with. Printable ASCII:
    ///     anything outside it is rare enough in this program's own text to be worth the
    ///     one-off cost on the frame that first draws it.
    /// </summary>
    private const string WarmCharacters =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    /// <summary>
    ///     When the home screen starts coming up, in seconds into the exit. The sound field
    ///     drains from the first frame, so it gets a head start on its own before anything
    ///     is drawn over it.
    /// </summary>
    private const float ExitFadeStart = 0.5f;

    private static AssetProvider _assetProvider = null!;
    private readonly AudioContext? _audioContext;
    private readonly DollarStoreLoaderBackground _background;
    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;

    private readonly LoaderInterface _loaderInterface;

    /// <summary>True on the first run: the setup is up, and it owns when loading starts.</summary>
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
    ///     The two warm-ups the scene builds depend on. Held rather than fired and forgotten
    ///     because building a scene is exactly what they make cheap - see
    ///     <see cref="QueuePreloadsWhenWarm" />.
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

        // Built here rather than when the download finishes: the sample holder and the
        // atlas store exist from the downloader's construction and fill in as the files
        // arrive, so the tool scenes can be handed this and built long before there is
        // anything in it.
        _workflow = new ThirtyDollarWorkflow(game, Logger, _thirtyDollarDownloader.SampleHolder,
            _thirtyDollarDownloader.AtlasStore, _audioContext);

        // The resampler is stored as a name plus its parameters, so it is rebuilt whenever
        // one of them moves rather than handed over once. Filtered by name because building
        // one is real work - the Kaiser tables are a few hundred thousand doubles - and the
        // greeting changing has nothing to do with it.
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

        // Started before either path below returns: on a returning boot these run against
        // the download, and on a first run against however long the setup is read for.
        WarmComponents();
        WarmFonts();

        _setupNeeded = !_settings.UpdateCheckAsked;
        if (!_setupNeeded)
        {
            if (_settings.CheckForUpdates)
                UpdateChecker.Start(_version, _settings.UpdateIncludePrereleases,
                    _settings.UpdateIncludeNightlies, Logger);

            // The sounds were agreed to once and the download skips whatever is already on
            // disk, so a returning boot needs no permission - only for the scenes to be up.
            _startRequested = true;
            _loaderInterface.BeginLoading();
            return;
        }

        // Default to the channel this build came from: a prerelease ticks prereleases, and a
        // nightly ticks both (the nightly workflow marks its releases prerelease as well).
        _loaderInterface.IncludePrereleases.Checked = _version?.Prerelease ?? false;
        _loaderInterface.IncludeNightlies.Checked = _version?.Nightly ?? false;

        // A build with no VERSION date (a developer build, or anything not out of a release
        // workflow) still gets the setup - it just isn't asked about updates, since there is
        // no build date to call a release newer than and the answer couldn't be used.
        _loaderInterface.BeginSetup(_version?.Date is not null);
    }

    /// <summary>
    ///     Compiles every screen's logic block, on a worker, before any of them is built.
    ///     <para>
    ///         Building a component is dominated by compiling its logic block - Roslyn, and
    ///         nothing else in the process comes close. Measured on this program's own
    ///         scenes: Editor 1104 ms, Home 394 ms, DrumMaster 362 ms, Settings 107 ms,
    ///         Visualizer 119 ms. None of it needs a graphics context, and all of it was
    ///         being paid on the render thread on the frames between the download finishing
    ///         and the home screen appearing.
    ///     </para>
    ///     <para>
    ///         Its own task, separate from <see cref="WarmFonts" />: they are both CPU and
    ///         there is no reason for the longer one to wait behind the shorter. The scene
    ///         builds wait for both - see <see cref="QueuePreloadsWhenWarm" />.
    ///     </para>
    /// </summary>
    private Task WarmComponents()
    {
        return ThreadRunner.RunTask(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            // A throwaway context: precompiling touches the markup parser, the asset
            // provider and the script cache, and none of the component registry that
            // makes a context worth keeping.
            var compiled = new SundexContext(_context).PrecompileLogic();

            Logger.Debug("[Component Warmup] Compiled {Count} logic blocks in {Elapsed} ms",
                compiled, stopwatch.ElapsedMilliseconds);
        });
    }

    /// <summary>
    ///     Generates every glyph the later screens will ask for, on a worker, before any of
    ///     them is built.
    ///     <para>
    ///         Putting a character on screen for the first time is two jobs: generating its
    ///         MSDF, and uploading that into the font atlas. Only the upload needs a graphics
    ///         context - the generation is ~1 ms of pure arithmetic per glyph, and it was
    ///         being paid synchronously inside a <c>Label</c> constructor on the render
    ///         thread. A screen introducing thirty new characters therefore cost about thirty
    ///         milliseconds, i.e. two dropped frames, before it drew anything.
    ///     </para>
    ///     <para>
    ///         The loading screen has cores to spare and a render thread that only has to
    ///         animate a strip, so the generation happens here instead and the scene builds
    ///         that follow are left with only the uploads.
    ///     </para>
    /// </summary>
    private Task WarmFonts()
    {
        return ThreadRunner.RunTask(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Three separate atlases, each with its own glyph cache: the interface's
                // own font, and the two the visualizer's text container and player bar
                // draw with. Twemoji is left alone - its glyph set is far too large to
                // warm speculatively, and no label in this program reaches for it.
                _context.TextProvider.Warm(WarmCharacters);
                Visualizer.VisualizerFonts.LatoRegularProvider.Warm(WarmCharacters);
                Visualizer.VisualizerFonts.LatoBoldProvider.Warm(WarmCharacters);
            }
            catch (Exception e)
            {
                // Warming is an optimisation, and a failure here must not take the boot
                // with it - every glyph it missed is simply generated on demand again.
                Logger.Debug("[Font Warmup] Failed with error: '{Exception}'", e);
                return;
            }

            Logger.Debug("[Font Warmup] Generated {Count} glyphs per atlas in {Elapsed} ms",
                WarmCharacters.Length, stopwatch.ElapsedMilliseconds);
        });
    }

    /// <summary>
    ///     Agrees to the download at the end of the setup. It does not begin here: the
    ///     scenes still have to be up first, which <see cref="StartDownloadWhenReady" />
    ///     waits for. This is also where the setup is marked done - a build with no date
    ///     never reaches the update question, so writing it with that answer would leave
    ///     those runs asking again on every boot.
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
        // UpdateCheckAsked is not written here - StartLoading owns it, so the setup only
        // counts as done once it has actually been finished.

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

            // sounds.json needs none of this and nothing needs it yet, so it goes out
            // now and is in hand by the time the download it feeds is allowed to start.
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

        // Guarded on Finished: this ran on every frame between the assets landing and the
        // transition taking effect.
        if (!Finished)
        {
            _loaderInterface.StatusMessage.SetTextContents("Opening the Visualizer");
            Finished = true;
            return;
        }

        UpdateExit();
    }

    /// <summary>
    ///     Holds the scene builds until the warm-ups they depend on have landed.
    ///     <para>
    ///         Both exist to make building a scene cheap - one compiles the layouts, the
    ///         other generates the glyphs their labels are spelt with - so starting the
    ///         builds alongside them would have each build blocking on the very work that
    ///         was meant to have finished first. Waiting costs nothing: the render thread is
    ///         free to animate this screen while the workers get on with it, which is the
    ///         one thing it cannot do while a scene is being built.
    ///     </para>
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
    ///     Queues every scene in <see cref="Preloads" /> to be built, a frame apart.
    ///     <para>
    ///         Two enqueued events per scene, because <see cref="Sundex.Engine.Game" /> runs
    ///         one per frame: the message gets a frame of its own and is on screen before the
    ///         build it describes takes the next one. Setting both in one event would leave
    ///         the previous scene's message up for the whole of this one's build.
    ///     </para>
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
    ///     Starts the sound download once there is nothing left for it to compete with: the
    ///     user has agreed to it, every scene is built, and sounds.json is in.
    ///     <para>
    ///         The scenes come first on purpose. Building them is render-thread work and
    ///         downloading is not, so running them together would have the loading screen
    ///         stuttering through the very progress it is drawing - and the download is the
    ///         long pole either way, so nothing is lost by letting the scenes go first.
    ///     </para>
    /// </summary>
    private void StartDownloadWhenReady()
    {
        if (_downloadStarted || !_startRequested) return;
        if (_preloadsDone < Preloads.Count) return;

        if (!_thirtyDollarDownloader.SampleListLoaded)
        {
            // The scenes are up and sounds.json is still out. Only reached on a slow
            // network - it is one small request against five scene builds - but the screen
            // should say what it is waiting for rather than leave the last scene's message
            // up over nothing.
            StatusUpdate(new LoadingSoundsListReport());
            return;
        }

        _downloadStarted = true;
        Logger.Debug("[Boot] {Count} scenes preloaded and the sound list is in - starting the download",
            Preloads.Count);
        _thirtyDollarDownloader.Load();
    }

    /// <summary>
    ///     The hand-off to <see cref="ExitTo" /> - the home screen unless --mode named
    ///     another. Program loads the scenes a frame apart, so this holds on the loading
    ///     screen until that one exists - the frames the other four cost land underneath
    ///     the animation instead of stacking into one stalled frame.
    ///     <br /><br />
    ///     The exit itself: the sound field stops bouncing and scatters off the top edge,
    ///     then the strip settles back to the zero height it rose from while the scene
    ///     underneath fades up through it. Home's playhead starts the moment it is
    ///     transitioned to, which is the moment the meter finished - one left-to-right
    ///     motion across the seam rather than two.
    /// </summary>
    private void UpdateExit()
    {
        if (_target is null)
        {
            // Every scene is built by the time the exit runs, so a miss here is a bad
            // --mode rather than a build still in flight: falling back beats holding the
            // loading screen up forever waiting for a scene nobody is going to add.
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
    ///     Fades the scene being handed off to, if it can be faded. A scene that can't
    ///     still gets the rest of the exit - the sound field scattering off the top, the
    ///     strip settling back down - it just arrives opaque underneath it.
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
using System.Diagnostics;
using LoadingScene.Background;
using LoadingScene.Reports;
using LoadingScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Updates;
using Sundex.Components;
using Sundex.Components.Markdown;
using Sundex.Components.Abstractions;
using Sundex.Engine;
using Sundex.Engine.Audio;
using Sundex.Engine.AssetManagement;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using Sundex.Engine.Text;
using ThirtyDollarConverter;
using VisualizerScene;
using VisualizerScene.Settings;
using Sundex.Engine.Input;

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
    ///     Seconds into the exit at which the target scene starts fading up. The sound field
    ///     drains from the first frame, so it moves before anything is drawn over it.
    /// </summary>
    private const float ExitFadeStart = 0.5f;

    private static AssetProvider _assetProvider = null!;
    private readonly AudioDevice? _audioDevice;
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
    ///     The font warm-up the scene builds depend on. It must have completed before a scene
    ///     is built - see <see cref="QueuePreloadsWhenWarm" />.
    /// </summary>
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

    /// <param name="workspace">Where the tool scenes open and save files.</param>
    /// <param name="samples">Where sounds download from and to; null for the site and the local cache.</param>
    public Loader(GameRuntime runtime, AudioDevice? audioDevice, VisualizerSettings settings, VersionInfo? version,
        Workspace workspace, SampleHolder? samples = null) : base(runtime)
    {
        _settings = settings;
        _version = version;

        var clientSize = runtime.ClientSize;
        if (runtime.TryGetScreenScale(out var scaleX, out var scaleY))
            _lastScale = new Vector2(scaleX, scaleY);

        _audioDevice = audioDevice;
        _camera = new DollarStoreCamera(Vector3.Zero, clientSize);

        _context = new UIContext
        {
            Camera = _camera,
            PixelScale = _lastScale,
            RequestCursor = type => _cursorType = type
        };
        _thirtyDollarDownloader = new ThirtyDollarDownloader(runtime.ThreadRunner, _assetProvider, samples)
        {
            StatusUpdate = StatusUpdate
        };

        // The sample holder and atlas store exist from the downloader's construction and
        // fill in as files arrive, so tool scenes can be handed this and built before the
        // download has produced anything.
        _workflow = new ThirtyDollarWorkflow(runtime, Logger, _thirtyDollarDownloader.SampleHolder,
            _thirtyDollarDownloader.AtlasStore, workspace, _audioDevice);

        // The resampler is stored as a name plus its parameters, so it is rebuilt whenever
        // one of them changes. Filtered by name because building one is expensive and most
        // settings have nothing to do with it.
        // The render-aheads are mirrored the same way; the workflow applies a change to the
        // song already playing.
        _workflow.EncoderSettings.Resampler = Resamplers.Create(settings);
        _workflow.RenderAheadSeconds = settings.VisualizerRenderAhead;
        _workflow.EditorRenderAheadSeconds = settings.EditorRenderAhead;
        _workflow.SequencePlayer.BlockFrames = settings.AudioBlockSize;
        _workflow.GpuMixing = settings.GpuAudioMixing;
        _workflow.EncoderSettings.CutFadeLengthMs = (uint)Math.Max(0, settings.CutFadeLength);
        settings.Changed += name =>
        {
            if (Resamplers.Properties.Contains(name))
                _workflow.EncoderSettings.Resampler = Resamplers.Create(settings);
            else if (name == nameof(VisualizerSettings.VisualizerRenderAhead))
                _workflow.RenderAheadSeconds = settings.VisualizerRenderAhead;
            else if (name == nameof(VisualizerSettings.EditorRenderAhead))
                _workflow.EditorRenderAheadSeconds = settings.EditorRenderAhead;
            else if (name == nameof(VisualizerSettings.AudioBlockSize))
                _workflow.SequencePlayer.BlockFrames = settings.AudioBlockSize;
            else if (name == nameof(VisualizerSettings.CutFadeLength))
                _workflow.EncoderSettings.CutFadeLengthMs = (uint)Math.Max(0, settings.CutFadeLength);
            else if (name == nameof(VisualizerSettings.GpuAudioMixing))
                _workflow.GpuMixing = settings.GpuAudioMixing;
        };

        _background = new DollarStoreLoaderBackground()
        {
            AtlasStore = _thirtyDollarDownloader.AtlasStore
        };
        _thirtyDollarDownloader.OnLoadSound = sound => _background.AddSound(sound);

        _loaderInterface = new LoaderInterface(_context)
        {
            OnUpdateAnswer = AnswerUpdatePrompt,
            OnStartLoading = StartLoading
        };

        // Started before either path below returns, so the warm-up overlaps the download on
        // a returning boot and the setup being read on a first run.
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
    ///     Builds each font's Slug glyph data on a worker, before any screen is built. A
    ///     font builds on first use and takes a few hundred milliseconds
    ///     (docs/handover/sundex-slug-plan.md), which would otherwise land on the render
    ///     thread with the first label. Only the texture upload that follows needs a graphics
    ///     context, and that stays on the render thread.
    /// </summary>
    private Task WarmFonts()
    {
        return ThreadRunner.RunTask(() =>
        {
            // The interface's own font and its icons, and the two the visualizer's text
            // container and player bar draw with.
            (string Name, ITextProvider Provider)[] fonts =
            [
                ("Lato Bold (interface)", _context.TextProvider),
                ("Lucide (icons)", _context.GetTextProvider(Icons.Font)),
                // Its own provider, not the visualizer's below: the update notes' first click
                // would otherwise pay for it.
                ("Lato Regular (Markdown)", _context.GetTextProvider(MarkdownView.BodyFont)),
                ("Lato Regular", Visualizer.VisualizerFonts.LatoRegularProvider),
                ("Lato Bold", Visualizer.VisualizerFonts.LatoBoldProvider)
            ];

            foreach (var (name, provider) in fonts)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    // The first glyph lookup is what builds the font's data.
                    provider.GetGlyph(" ");
                }
                catch (Exception e)
                {
                    // Warming is an optimisation: a failure must not fail the boot. The
                    // same failure will surface on the frame that first draws this font.
                    Logger.Debug("[Font Warmup] {Font} failed with error: '{Exception}'", name, e);
                    continue;
                }

                Logger.Debug("[Font Warmup] Built Slug data for {Font} in {Elapsed} ms",
                    name, stopwatch.ElapsedMilliseconds);
            }
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
        _background.Render(renderArgs.Commands);
        _context.Render(renderArgs.Commands);
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

            _fontWarm = WarmFonts();

            StatusUpdate(new ScenePreloadReport
            {
                Message = "Preparing the interface",
                Detail = "loading fonts"
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

        if (Runtime.Host.Cursor != cursor)
            Runtime.Host.Cursor = cursor;

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
    ///     Holds the scene builds until the font warm-up has landed, so a build never blocks
    ///     on the work meant to make it cheap. The render thread animates this screen while
    ///     the worker runs, which it cannot do while a scene is being built.
    /// </summary>
    private void QueuePreloadsWhenWarm()
    {
        if (_preloadsQueued) return;
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

            Runtime.Enqueue(_ => StatusUpdate(new ScenePreloadReport
            {
                Message = preload.Message,
                Detail = $"{step + 1} of {Preloads.Count}",
                Percentage = (step + 1) / (double)Preloads.Count
            }));

            Runtime.Enqueue(runtime =>
            {
                preload.Load(runtime, _workflow);
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
        // The scene is fully up and taking input from here; viz.sh start waits on this line.
        Logger.Information("[Boot] Handed off to {Scene}", ExitTo);
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
        if (Runtime.TryGetScreenScale(out var scaleX, out var scaleY))
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

    public override void Keyboard(KeyboardInput state)
    {
    }

    public override void Mouse(PointerInput mouseState, KeyboardInput keyboardState)
    {
        _loaderInterface.MouseEvent(mouseState, _lastScale);
    }
}

using System.Diagnostics;
using Shared.Atlases;
using Shared.Audio;
using Shared.Audio.Gpu;
using Shared.Audio.Null;
using Shared.Objects;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Encoder.Resamplers;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using ThirtyDollarConverter.Voices;
using ILogger = Serilog.ILogger;

namespace Shared;

public class ThirtyDollarWorkflow
{
    private const int ModifiedSequenceUpdateIntervalMs = 250;

    /// <summary>The range the two render-aheads below are clamped to - a settings file can hold anything.</summary>
    public const double MinRenderAheadSeconds = 0.25, MaxRenderAheadSeconds = 30;

    private readonly AssetInfo _assetInfo = new()
    {
        Storage = StorageLocation.Disk
    }; // cached to avoid re-allocating each update

    private readonly AssetProvider _assetProvider;
    private readonly Stopwatch _audioLog = Stopwatch.StartNew();
    private int _audioLogFrames;
    private double _audioLogFrameMax;
    private readonly Stopwatch _fileUpdateStopwatch;

    /// <summary>Called after the sequence has finished loading, but before the audio events have finished processing.</summary>
    public Func<TimedEvents, SequencePlayer, Task>? HandleAfterSequenceLoad;

    /// <summary>
    ///     How far ahead of the playhead the visualizer mixes: long enough that no garbage
    ///     collection outlasts it. Only file reloads replace the song here, and they take over at
    ///     the playhead either way. Setting it changes the song already playing, if it is ours.
    /// </summary>
    public double RenderAheadSeconds
    {
        get;
        set
        {
            field = Math.Clamp(value, MinRenderAheadSeconds, MaxRenderAheadSeconds);
            SequencePlayer.RefreshAheadSeconds();
        }
    } = 10;

    /// <summary>
    ///     How far ahead of the playhead the editor mixes. Short, so an edit or a seek refills
    ///     little - a new list replaces the queued audio anyway - but past any garbage
    ///     collection. Lives here because the editor plays through this workflow's player.
    /// </summary>
    public double EditorRenderAheadSeconds
    {
        get;
        set
        {
            field = Math.Clamp(value, MinRenderAheadSeconds, MaxRenderAheadSeconds);
            SequencePlayer.RefreshAheadSeconds();
        }
    } = 2;

    /// <summary>The last build, kept for its resampled sounds: a reload of the same files reuses them.</summary>
    private VoiceRender? _render;

    public ThirtyDollarWorkflow(Game game, ILogger logger, SampleHolder sampleHolder, AtlasStore atlasStore,
        AudioContext? context = null)
    {
        _assetProvider = game.AssetProvider;
        Game = game;
        Logger = logger.ForContext<ThirtyDollarWorkflow>();
        SequencePlayer = new SequencePlayer(logger, context);
        AtlasStore = atlasStore;

        // The song can mix on the GPU when the context has compute shaders and there is something
        // to hear; a --no-audio run keeps the CPU mixer. GpuMixing switches it.
        if (game.GLInfo.SupportsComputeShaders && SequencePlayer.AudioContext is not NullAudioContext)
        {
            var mixer = GpuMixer = new GpuVoiceMixer(logger, game.AssetProvider);
            SequencePlayer.CursorFactory = mixer.CreateCursor;
            game.UpdateFrame += _ => mixer.Pump();
        }

        game.UpdateFrame += frame => LogAudio(frame.Time);
        SampleHolder = sampleHolder;

        EncoderSettings = new EncoderSettings
        {
            SampleRate = (uint)SequencePlayer.AudioContext.SampleRate,
            Channels = 2,
            Resampler = new HermiteResampler(),
            EnableNormalization = false,
            CutFadeLengthMs = 4
        };

        _fileUpdateStopwatch = new Stopwatch();
        _fileUpdateStopwatch.Start();
    }

    public EncoderSettings EncoderSettings { get; }

    public TimedEvents TimedEvents { get; } = new()
    {
        Placement = [],
        TimingSampleRate = 100_000
    };

    public AtlasStore AtlasStore { get; }
    public SampleHolder SampleHolder { get; }
    public Game Game { get; }
    public ILogger Logger { get; }
    public SequencePlayer SequencePlayer { get; }

    /// <summary>Mixes the player's song on the GPU, once per frame; null when the context can't.</summary>
    public GpuVoiceMixer? GpuMixer { get; }

    /// <summary>
    ///     Whether the song mixes on the GPU where it can: the "Mix audio on the GPU" setting,
    ///     applied to the song already playing from its next block.
    /// </summary>
    public bool GpuMixing
    {
        get => GpuMixer?.Enabled ?? false;
        set => GpuMixer?.Enabled = value;
    }

    private bool AutoUpdate { get; set; } = true;
    public bool ShowDebugInfo { get; set; }
    public SequenceIndices SequenceIndices { get; private set; } = new();
    public SequenceInfo[] SequenceInfos { get; private set; } = [];
    public Placement[] ExtractedSpeedEvents { get; private set; } = [];

    /// <summary>
    ///     Once a second while playing, Debug builds log the mixer's state - what a headless run
    ///     checks playback against (<c>viz.sh wait '\[Audio\]'</c>): the playhead, the feeder's
    ///     CPU load, the GPU's time per second of audio, blocks the CPU took over, underruns, and
    ///     the frame times over that second - the mixer shares the GPU with the frame's draws.
    /// </summary>
    [Conditional("DEBUG")]
    private void LogAudio(double frameSeconds)
    {
        var clock = SequencePlayer.GetTimingStopwatch();
        if (!clock.IsRunning) return;

        _audioLogFrames++;
        _audioLogFrameMax = Math.Max(_audioLogFrameMax, frameSeconds);
        if (_audioLog.ElapsedMilliseconds < 1000) return;

        Logger.Debug(
            "[Audio] at={At:0.0}s cpu={Cpu:0.0}% gpu={Gpu:0.00}ms/s takeovers={Takeovers} underruns={Underruns} " +
            "frame_mean={Mean:0.00}ms frame_max={Max:0.00}ms",
            clock.ElapsedMilliseconds / 1000.0, SequencePlayer.CpuLoad * 100, GpuMixer?.MsPerSecond ?? 0,
            GpuMixer?.Takeovers ?? 0, SequencePlayer.Underruns,
            _audioLog.Elapsed.TotalMilliseconds / _audioLogFrames, 1000 * _audioLogFrameMax);
        _audioLog.Restart();
        _audioLogFrames = 0;
        _audioLogFrameMax = 0;
    }

    /// <summary>
    ///     This method updates the current sequence.
    /// </summary>
    /// <param name="locations">The location of the sequences you want to use.</param>
    /// <param name="restartPlayer">Whether to restart the sequence from the beginning.</param>
    public async Task UpdateSequences(string?[] locations, bool restartPlayer = true)
    {
        var sequence_array = new Sequence[locations.Length];
        SequenceInfos = GetSequenceInfos(locations);
        if (SequenceInfos.Length < 1)
        {
            Logger.Debug(
                "[Sequence Update] No valid files were dropped on the window. If dragging a folder, drag the files inside it");
            return;
        }

        for (var index = 0; index < SequenceInfos.Length; index++)
        {
            var sequence_info = SequenceInfos[index];
            var sequence = Sequence.FromString(_assetProvider.LoadString(sequence_info.FileLocation));
            sequence_array[index] = sequence;
        }

        await UpdateSequences(sequence_array, restartPlayer);
    }

    /// <summary>
    ///     This method updates the current sequence.
    /// </summary>
    /// <param name="sequences">The sequences you want to use.</param>
    /// <param name="restartPlayer">Whether to restart the sequence from the beginning.</param>
    public async Task UpdateSequences(Sequence[] sequences, bool restartPlayer = true)
    {
        AutoUpdate = true;
        lock (ExtractedSpeedEvents)
        {
            ExtractedSpeedEvents = [];
        }

        var audio_context = SequencePlayer.GetContext();
        var updateRate = audio_context.SampleRate;

        if (restartPlayer)
            SequencePlayer.Stop();

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = (uint)updateRate,
            AddVisualEvents = true
        });

        var placement = calculator.CalculateMany(sequences).ToArray();
        SequenceIndices = GenerateSequenceIndexes(placement);
        TimedEvents.TimingSampleRate = updateRate;
        TimedEvents.Placement = placement;
        TimedEvents.Sequences = sequences;

        SequencePlayer.ClearSubscriptions();
        if (HandleAfterSequenceLoad != null)
            await HandleAfterSequenceLoad(TimedEvents, SequencePlayer);


        // A new song resamples afresh - the resampler may have changed since - while a reload
        // of the same files only resamples what the edit changed. The voices come from the
        // placement above, visual events and all: placing a long song twice costs gigabytes.
        var pcm_encoder = new PcmEncoder(SampleHolder, EncoderSettings);
        var render = await pcm_encoder.BuildVoices(new TimedEvents
        {
            Sequences = sequences,
            Placement = placement,
            TimingSampleRate = updateRate
        }, restartPlayer ? null : _render?.ProcessedEvents);
        var previous = _render;
        _render = render;
        previous?.Dispose();

        // the player's own reference, taken here: this render may be replaced before it starts
        render.Voices.Retain();

        _ = Game.ThreadRunner.RunTask(UpdateExtractedSpeedEvents);
        Game.ThreadRunner.RunThread(() => UpdateAndStart(render.Voices, restartPlayer));
    }

    private void UpdateAndStart(VoiceList voices, bool restartPlayer)
    {
        SequencePlayer.UpdateSequence(TimedEvents, SequenceIndices, voices, () => RenderAheadSeconds);
        if (restartPlayer)
            SequencePlayer.Start(Game.ThreadRunner);
        else SequencePlayer.AlignToTime();
    }

    public static SequenceIndices GenerateSequenceIndexes(IEnumerable<Placement> placements)
    {
        var ends = placements.Where(p => p.Event is EndEvent)
            .Select((end, i) => (end.Index, i))
            .ToArray();

        return new SequenceIndices
        {
            Ends = ends
        };
    }

    public SequenceInfo[] GetSequenceInfos(IEnumerable<string?> locations)
    {
        return
        [
            .. locations.Where(l => File.Exists(l) && !Directory.Exists(l)).Select(l => new SequenceInfo
            {
                FileLocation = l!,
                FileModifiedTime = _assetProvider.Metadata<AssetMetadata, AssetInfo>(new AssetInfo
                        { Location = l!, Storage = StorageLocation.Disk })
                    .ModifiedDate
            })
        ];
    }

    private void UpdateExtractedSpeedEvents()
    {
        lock (ExtractedSpeedEvents)
        {
            ExtractedSpeedEvents = [.. TimedEvents.Placement.Where(p => p.Event.SoundEvent is "!speed")];
        }
    }

    public void Update()
    {
        AtlasStore.Update();

        if (_fileUpdateStopwatch.ElapsedMilliseconds < ModifiedSequenceUpdateIntervalMs)
            return;

        HandleIfSequenceUpdate();
        _fileUpdateStopwatch.Restart();
    }

    private void HandleIfSequenceUpdate()
    {
        if (SequenceInfos.Length < 1 || !AutoUpdate) return;
        var anyChanged = false;
        foreach (var sequence_info in SequenceInfos.AsSpan())
        {
            _assetInfo.Location = sequence_info.FileLocation;
            var recorded_m_time = sequence_info.FileModifiedTime;
            var metadata = _assetProvider.Metadata<AssetMetadata, AssetInfo>(_assetInfo);

            if (!metadata.Found)
            {
                AutoUpdate = false;
                Logger.Debug(
                    "[Auto Update] One of the sequences was deleted. \n" +
                    "Disabling auto-reload until the next manual update");

                return;
            }

            if (recorded_m_time == metadata.ModifiedDate) continue;
            anyChanged = true;
            break;
        }

        if (!anyChanged) return;

        try
        {
            UpdateSequences([.. SequenceInfos.Select(s => s.FileLocation).Where(File.Exists)], false)
                .GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Logger.Debug("[Sequence Loader] Failed to load sequence with error: '{Exception}'", e);
        }
    }
}
using System.Diagnostics;
using Shared.Atlases;
using Shared.Audio;
using Shared.Objects;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Encoder.Resamplers;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using ILogger = Serilog.ILogger;
using StringInfo = Sundex.Engine.Asset_Management.Types.String.StringInfo;

namespace Shared;

public class ThirtyDollarWorkflow
{
    private const int ModifiedSequenceUpdateIntervalMs = 250;

    private readonly AssetInfo _assetInfo = new()
    {
        Storage = StorageLocation.Disk
    }; // cached to avoid re-allocating each update

    private readonly AssetProvider _assetProvider;
    private readonly Stopwatch _fileUpdateStopwatch;

    /// <summary>Called after the sequence has finished loading, but before the audio events have finished processing.</summary>
    public Func<TimedEvents, SequencePlayer, Task>? HandleAfterSequenceLoad;

    private RenderedSequence? _renderedSequence;

    public ThirtyDollarWorkflow(Game game, ILogger logger, SampleHolder sampleHolder, AtlasStore atlasStore,
        AudioContext? context = null)
    {
        _assetProvider = game.AssetProvider;
        Game = game;
        Logger = logger.ForContext<ThirtyDollarWorkflow>();
        SequencePlayer = new SequencePlayer(logger, context);
        AtlasStore = atlasStore;
        SampleHolder = sampleHolder;

        EncoderSettings = new EncoderSettings
        {
            SampleRate = (uint)SequencePlayer.AudioContext.SampleRate,
            Channels = 2,
            Resampler = new HannSincResampler(),
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

    public AtlasStore AtlasStore { get; init; }
    public SampleHolder SampleHolder { get; init; }
    public Game Game { get; }
    public ILogger Logger { get; }
    public SequencePlayer SequencePlayer { get; }

    public bool AutoUpdate { get; set; } = true;
    public bool ShowDebugInfo { get; set; }
    public SequenceIndices SequenceIndices { get; private set; } = new();
    public SequenceInfo[] SequenceInfos { get; private set; } = [];
    public Placement[] ExtractedSpeedEvents { get; private set; } = [];

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
            var asset = _assetProvider.Load<StringAsset, StringInfo>(
                StringInfo.CreateFromUnknownStorage(sequence_info.FileLocation));
            var sequence = Sequence.FromString(asset.Value);
            sequence_array[index] = sequence;
        }

        if (restartPlayer)
            _renderedSequence = null;
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


        var pcm_encoder = new PcmEncoder(SampleHolder, EncoderSettings);
        _renderedSequence = _renderedSequence == null
            ? await pcm_encoder.GetMultipleSequencesAudio(sequences)
            : await pcm_encoder.ComputeIncrementalAudio(_renderedSequence, sequences);

        _ = Game.ThreadRunner.RunTask(UpdateExtractedSpeedEvents);
        Game.ThreadRunner.RunThread(() => UpdateAndStart(_renderedSequence, restartPlayer));
    }

    private void UpdateAndStart(RenderedSequence renderedSequence, bool restartPlayer)
    {
        SequencePlayer.UpdateSequence(TimedEvents, SequenceIndices, renderedSequence, restartPlayer);
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
        return locations.Where(l => File.Exists(l) && !Directory.Exists(l)).Select(l => new SequenceInfo
        {
            FileLocation = l!,
            FileModifiedTime = _assetProvider.Metadata<AssetMetadata, AssetInfo>(new AssetInfo
                    { Location = l!, Storage = StorageLocation.Disk })
                .ModifiedDate
        }).ToArray();
    }

    public void UpdateExtractedSpeedEvents()
    {
        lock (ExtractedSpeedEvents)
        {
            ExtractedSpeedEvents = TimedEvents.Placement.Where(p => p.Event.SoundEvent is "!speed").ToArray();
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
            UpdateSequences(SequenceInfos.Select(s => s.FileLocation).Where(File.Exists).ToArray(), false)
                .GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Logger.Debug("[Sequence Loader] Failed to load sequence with error: '{Exception}'", e);
        }
    }
}
using System.Diagnostics;
using System.Globalization;
using Shared.Atlases;
using Shared.Audio;
using Shared.Objects;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.Resamplers;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ILogger = Serilog.ILogger;
using StringInfo = Sundex.Engine.Asset_Management.Types.String.StringInfo;

namespace Shared;

public class ThirtyDollarWorkflow
{
    private readonly Stopwatch _fileUpdateStopwatch;
    private readonly AssetProvider _assetProvider;
    private const int ModifiedSequenceUpdateIntervalMs = 250;

    public ThirtyDollarWorkflow(Game game, ILogger logger, AudioContext? context = null)
    {
        _assetProvider = game.AssetProvider;
        Game = game;
        Log = logger.ForContext<ThirtyDollarWorkflow>();
        SequencePlayer = new SequencePlayer(logger, context);
        _fileUpdateStopwatch = new Stopwatch();
        _fileUpdateStopwatch.Start();
    }

    /// <summary>Called after the sequence has finished loading, but before the audio events have finished processing.</summary>
    public Func<TimedEvents, SequencePlayer, Task>? HandleAfterSequenceLoad;

    public TimedEvents TimedEvents { get; } = new()
    {
        Placement = [],
        TimingSampleRate = 100_000,
    };

    public required AtlasStore AtlasStore { get; init; }
    public required SampleHolder SampleHolder { get; init; }
    public Game Game { get; }
    public ILogger Log { get; }
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
            Log.Debug(
                "[Sequence Update] No valid files were dropped on the window. If dragging a folder, drag the files inside it.");
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

        const int updateRate = 100_000;

        if (restartPlayer)
            await SequencePlayer.Stop();

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = updateRate,
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

        var audio_context = SequencePlayer.GetContext();
        var pcm_encoder = new PcmEncoder(SampleHolder, new EncoderSettings
        {
            SampleRate = (uint)audio_context.SampleRate,
            Channels = 2,
            Resampler = new HermiteResampler()
        });

        var samples = await pcm_encoder.GetAudioSamples(TimedEvents);
        var buffer_holder = new BufferHolder();

        foreach (var ev in samples)
        {
            var val = ev.Value;
            var value = val.Value;
            var name = val.Name ?? string.Empty;

            if (buffer_holder.ProcessedBuffers.TryGetValue(name, out var event_buffers))
                if (event_buffers.ContainsKey(value))
                    continue;

            var sample = audio_context.GetBufferObject(val.AudioData, audio_context.SampleRate);
            if (event_buffers != null)
            {
                event_buffers.Add(value, sample);
                continue;
            }

            buffer_holder.ProcessedBuffers.Add(name, new Dictionary<double, AudibleBuffer>
            {
                { value, sample }
            });
        }

        _ = Task.Run(UpdateExtractedSpeedEvents);

        await SequencePlayer.UpdateSequence(buffer_holder, TimedEvents, SequenceIndices);

        if (restartPlayer)
            await SequencePlayer.Start(Game.ThreadRunner);
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
            FileModifiedTime = _assetProvider.Metadata<AssetMetadata, AssetInfo>(new AssetInfo { Location = l! })
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

    private readonly AssetInfo _assetInfo = new(); // cached to avoid re-allocating each update

    private void HandleIfSequenceUpdate()
    {
        if (SequenceInfos.Length < 1 || !AutoUpdate) return;
        foreach (var sequence_info in SequenceInfos.AsSpan())
        {
            _assetInfo.Location = sequence_info.FileLocation;
            var recorded_m_time = sequence_info.FileModifiedTime;
            var metadata = _assetProvider.Metadata<AssetMetadata, AssetInfo>(_assetInfo);

            if (!metadata.Found)
            {
                AutoUpdate = false;
                Log.Debug(
                    "[Auto Update] One of the sequences was deleted. \n" +
                    "Disabling auto-reload until the next manual update.");

                return;
            }

            if (recorded_m_time != metadata.ModifiedDate) break;
            return;
        }

        try
        {
            UpdateSequences(SequenceInfos.ToArray().Select(s => s.FileLocation).Where(File.Exists).ToArray(), false)
                .GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Log.Debug("[Sequence Loader] Failed to load sequence with error: '{Exception}'", e);
        }
    }
}
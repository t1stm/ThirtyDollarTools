using Shared.Atlases;
using Shared.Audio;
using Shared.Objects;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.Resamplers;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using Sundex.Engine;
using ILogger = Serilog.ILogger;

namespace Shared;

public class ThirtyDollarWorkflow(Game game, ILogger logger, AudioContext? context = null)
{
    public bool AutoUpdate = true;

    public Placement[] ExtractedSpeedEvents = [];

    /// <summary>
    ///     Called after the sequence has finished loading, but before the audio events have finished processing.
    /// </summary>
    public Func<TimedEvents, SequencePlayer, Task>? HandleAfterSequenceLoad;

    public TimedEvents TimedEvents = new()
    {
        Placement = [],
        TimingSampleRate = 100_000
    };

    public Game Game { get; } = game;
    public ILogger Log { get; set; } = logger.ForContext<ThirtyDollarWorkflow>();
    public required AtlasStore AtlasStore { get; set; }
    public required SampleHolder SampleHolder { get; set; }
    public SequencePlayer SequencePlayer { get; } = new(logger, context);
    public bool ShowDebugInfo { get; set; }
    public SequenceIndices SequenceIndices { get; private set; } = new();
    public Memory<SequenceInfo> Sequences { get; private set; } = Array.Empty<SequenceInfo>();

    /// <summary>
    ///     This method updates the current sequence.
    /// </summary>
    /// <param name="locations">The location of the sequences you want to use.</param>
    /// <param name="restartPlayer">Whether to restart the sequence from the beginning.</param>
    public async Task UpdateSequences(string?[] locations, bool restartPlayer = true)
    {
        var sequence_array = new Sequence[locations.Length];
        var i = 0;
        Sequences = GetSequenceInfos(locations);
        if (Sequences.Length < 1)
        {
            Log.Debug(
                "[Sequence Update] No valid files were dropped on the window. If dragging a folder, drag the files inside it.");
            return;
        }

        for (var index = 0; index < Sequences.Span.Length; index++)
        {
            var sequence_info = Sequences.Span[index];
            var read = await File.ReadAllTextAsync(sequence_info.FileLocation);
            var sequence = Sequence.FromString(read);
            sequence_array[i++] = sequence;
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

    public static SequenceInfo[] GetSequenceInfos(IEnumerable<string?> locations)
    {
        return locations.Where(l => File.Exists(l) && !Directory.Exists(l)).Select(l => new SequenceInfo
        {
            FileLocation = l!,
            FileModifiedTime = File.GetLastWriteTime(l!)
        }).ToArray();
    }

    public void UpdateExtractedSpeedEvents()
    {
        lock (ExtractedSpeedEvents)
        {
            ExtractedSpeedEvents = TimedEvents.Placement.Where(p => p.Event.SoundEvent is "!speed").ToArray();
        }
    }

    /// <summary>
    ///     Call this when you want to check if the sequence is updated and you want to update it if it is.
    /// </summary>
    public void HandleIfSequenceUpdate()
    {
        if (Sequences.Length < 1 || !AutoUpdate) return;
        foreach (var sequence_info in Sequences.Span)
        {
            var filename = sequence_info.FileLocation;
            var recorded_m_time = sequence_info.FileModifiedTime;
            if (!File.Exists(filename))
            {
                AutoUpdate = false;
                Log.Debug(
                    "[Auto Update] One of the sequences was deleted. \n" +
                    "Disabling auto-reload until the next manual update.");

                return;
            }

            var m_time = File.GetLastWriteTime(filename);
            if (recorded_m_time != m_time) break;
            return;
        }

        try
        {
            Log.Debug("[Auto Update] Recalculating all sequences.");
            UpdateSequences(Sequences.ToArray().Select(s => s.FileLocation).Where(File.Exists).ToArray(), false)
                .GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Log.Debug("[Sequence Loader] Failed to load sequence with error: '{Exception}'", e);
        }
    }
}
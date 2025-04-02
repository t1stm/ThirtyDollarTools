using ThirtyDollarConverter;
using ThirtyDollarConverter.Audio.Resamplers;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Audio;
using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Scenes;

public abstract class ThirtyDollarWorkflow
{
    private readonly SemaphoreSlim SampleHolderLock = new(1);
    protected readonly SequencePlayer SequencePlayer;
    protected bool AutoUpdate = true;
    protected bool Debug;

    protected Placement[] ExtractedSpeedEvents = [];
    protected Action<string> Log;
    protected SampleHolder? SampleHolder;
    protected SequenceIndices SequenceIndices = new();
    protected Memory<SequenceInfo> Sequences = Array.Empty<SequenceInfo>();

    protected TimedEvents TimedEvents = new()
    {
        Placement = [],
        TimingSampleRate = 100_000
    };

    public ThirtyDollarWorkflow(AudioContext? context = null, Action<string>? logging_action = null)
    {
        SequencePlayer = new SequencePlayer(context);
        Log = logging_action ?? (log => { Console.WriteLine($"({DateTime.Now:G}): {log}"); });
    }

    /// <summary>
    ///     Creates the sample holder.
    /// </summary>
    protected async Task CreateSampleHolder()
    {
        SampleHolder = new SampleHolder
        {
            DownloadUpdate = (sample, current, count) => { Log($"({current} - {count}): Downloaded: \'{sample}\'"); }
        };

        Log("[Sample Holder] Loading...");

        await SampleHolder.LoadSampleList();
        SampleHolder.PrepareDirectory();
        await SampleHolder.DownloadSamples();
        await SampleHolder.DownloadImages();
        SampleHolder.LoadSamplesIntoMemory();

        Log("[Sample Holder] Loaded all samples and images.");
    }

    protected async Task<SampleHolder> GetSampleHolder()
    {
        try
        {
            await SampleHolderLock.WaitAsync();
            if (SampleHolder == null) await CreateSampleHolder();
            return SampleHolder!;
        }
        finally
        {
            SampleHolderLock.Release();
        }
    }

    /// <summary>
    ///     This method updates the current sequence.
    /// </summary>
    /// <param name="locations">The location of the sequences you want to use.</param>
    /// <param name="restart_player">Whether to restart the sequence from the beginning.</param>
    protected virtual async Task UpdateSequences(string?[] locations, bool restart_player = true)
    {
        var sequence_array = new Sequence[locations.Length];
        var i = 0;
        Sequences = GetSequenceInfos(locations);
        if (Sequences.Length < 1)
        {
            Log(
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

        await UpdateSequences(sequence_array, restart_player);
    }

    /// <summary>
    ///     This method updates the current sequence.
    /// </summary>
    /// <param name="sequences">The sequences you want to use.</param>
    /// <param name="restart_player">Whether to restart the sequence from the beginning.</param>
    public virtual async Task UpdateSequences(Sequence[] sequences, bool restart_player = true)
    {
        AutoUpdate = true;
        lock (ExtractedSpeedEvents)
        {
            ExtractedSpeedEvents = [];
        }

        const int update_rate = 100_000;

        if (restart_player)
            await SequencePlayer.Stop();

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = update_rate,
            AddVisualEvents = true
        });

        var placement = calculator.CalculateMany(sequences).ToArray();
        SequenceIndices = GenerateSequenceIndexes(placement);
        TimedEvents.TimingSampleRate = update_rate;
        TimedEvents.Placement = placement;
        TimedEvents.Sequences = sequences;

        await HandleAfterSequenceLoad(TimedEvents);
        SequencePlayer.ClearSubscriptions();
        SetSequencePlayerSubscriptions(SequencePlayer);

        var sample_holder = await GetSampleHolder();

        var audio_context = SequencePlayer.GetContext();
        var pcm_encoder = new PcmEncoder(sample_holder, new EncoderSettings
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
                {value, sample}
            });
        }

        _ = Task.Run(UpdateExtractedSpeedEvents);
        await SequencePlayer.UpdateSequence(buffer_holder, TimedEvents, SequenceIndices);

        if (restart_player)
            await SequencePlayer.Start();
    }

    protected static SequenceIndices GenerateSequenceIndexes(IEnumerable<Placement> placements)
    {
        var ends = placements.Where(p => p.Event is EndEvent)
            .Select((end, i) => (end.Index, i))
            .ToArray();

        return new SequenceIndices
        {
            Ends = ends
        };
    }

    protected static SequenceInfo[] GetSequenceInfos(IEnumerable<string?> locations)
    {
        return locations.Where(l => File.Exists(l) && !Directory.Exists(l)).Select(l => new SequenceInfo
        {
            FileLocation = l!,
            FileModifiedTime = File.GetLastWriteTime(l!)
        }).ToArray();
    }

    private void UpdateExtractedSpeedEvents()
    {
        lock (ExtractedSpeedEvents)
        {
            ExtractedSpeedEvents = TimedEvents.Placement.Where(p => p.Event.SoundEvent is "!speed").ToArray();
        }
    }

    /// <summary>
    ///     Called after the sequence has finished loading, but before the audio events have finished processing.
    /// </summary>
    /// <param name="events">The events the sequence contains.</param>
    protected abstract Task HandleAfterSequenceLoad(TimedEvents events);

    /// <summary>
    ///     Called by the abstract class in order to use the implementation, when the SequencePlayer is created.
    /// </summary>
    /// <param name="player">The created SequencePlayer.</param>
    protected abstract void SetSequencePlayerSubscriptions(SequencePlayer player);

    /// <summary>
    ///     Call this when you want to check if the sequence is updated and you want to update it if it is.
    /// </summary>
    protected virtual void HandleIfSequenceUpdate()
    {
        if (Sequences.Length < 1 || !AutoUpdate) return;
        foreach (var sequence_info in Sequences.Span)
        {
            var filename = sequence_info.FileLocation;
            var recorded_m_time = sequence_info.FileModifiedTime;
            if (!File.Exists(filename))
            {
                AutoUpdate = false;
                Log(
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
            Log("[Auto Update] Recalculating all sequences.");
            UpdateSequences(Sequences.ToArray().Select(s => s.FileLocation).Where(File.Exists).ToArray(), false)
                .GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Log($"[Sequence Loader] Failed to load sequence with error: \'{e}\'");
        }
    }
}
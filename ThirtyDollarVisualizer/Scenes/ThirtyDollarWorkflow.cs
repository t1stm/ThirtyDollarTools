using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Resamplers;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Audio;

namespace ThirtyDollarVisualizer.Scenes;

public abstract class ThirtyDollarWorkflow
{
    private readonly SemaphoreSlim SampleHolderLock = new(1);
    protected readonly SequencePlayer SequencePlayer;
    protected DateTime _sequence_date_modified = DateTime.MinValue;

    protected string? _sequence_location;
    protected Action<string> Log;
    protected SampleHolder? SampleHolder;

    protected TimedEvents TimedEvents = new()
    {
        Placement = Array.Empty<Placement>(),
        TimingSampleRate = 100_000
    };

    protected Placement[] ExtractedSpeedEvents = Array.Empty<Placement>();

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

        Log("[Sample Holder] Loading.");

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
    /// <param name="location">The location of the sequence you want to use.</param>
    /// <param name="restart_player">Whether to restart the sequence from the beginning.</param>
    public virtual async Task UpdateSequence(string location, bool restart_player = true)
    {
        var file_data = await File.ReadAllTextAsync(location);
        var sequence = Sequence.FromString(file_data);
        await UpdateSequence(sequence, restart_player);
        _sequence_date_modified = File.GetLastWriteTime(location);
        _sequence_location = location;
    }

    /// <summary>
    ///     This method updates the current sequence.
    /// </summary>
    /// <param name="sequence">The sequence you want to use.</param>
    /// <param name="restart_player">Whether to restart the sequence from the beginning.</param>
    public virtual async Task UpdateSequence(Sequence sequence, bool restart_player = true)
    {
        lock (ExtractedSpeedEvents)
        {
            ExtractedSpeedEvents = Array.Empty<Placement>();
        }
        
        const int update_rate = 100_000;
        _sequence_location = null;

        if (restart_player)
            await SequencePlayer.Stop();

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = update_rate,
            AddVisualEvents = true
        });

        var placement = calculator.CalculateOne(sequence).ToArray();
        TimedEvents.TimingSampleRate = update_rate;
        TimedEvents.Placement = placement;
        TimedEvents.Sequence = sequence;

        HandleAfterSequenceLoad(TimedEvents);
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

            if (buffer_holder.ProcessedBuffers.ContainsKey((name, value)))
                continue;

            var sample = audio_context.GetBufferObject(val.AudioData, audio_context.SampleRate);
            buffer_holder.ProcessedBuffers.Add((name, value), sample);
        }
        
        _ = Task.Run(UpdateExtractedSpeedEvents);
        await SequencePlayer.UpdateSequence(buffer_holder, TimedEvents);

        if (restart_player)
            await SequencePlayer.Start();
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
    protected abstract void HandleAfterSequenceLoad(TimedEvents events);

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
        if (_sequence_location == null) return;
        if (!File.Exists(_sequence_location))
        {
            _sequence_location = null;
            Log("Current sequence files was moved or deleted. Disabling hot-reloading.");
            return;
        }
        
        var modify_time = File.GetLastWriteTime(_sequence_location);

        if (_sequence_date_modified == modify_time) return;
        try
        {
            UpdateSequence(_sequence_location, false).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Log($"[Sequence Loader] Failed to load sequence with error: \'{e}\'");
        }
    }
}
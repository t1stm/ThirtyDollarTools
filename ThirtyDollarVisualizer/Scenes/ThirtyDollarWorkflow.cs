using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Resamplers;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Audio;

namespace ThirtyDollarVisualizer.Scenes;

public abstract class ThirtyDollarWorkflow(Action<string>? logging_action = null)
{
    protected readonly SequencePlayer SequencePlayer = new();
    protected readonly Action<string> Log = logging_action ?? (log => { Console.WriteLine($"({DateTime.Now:G}): {log}"); });
    protected SampleHolder? SampleHolder;
    protected TimedEvents TimedEvents = new()
    {
        Placement = Array.Empty<Placement>(),
        TimingSampleRate = 100_000
    };

    protected string? _sequence_location;
    protected DateTime _sequence_date_modified = DateTime.MinValue;

    public ThirtyDollarWorkflow(AudioContext? context) : this()
    {
        SequencePlayer = new SequencePlayer(context);
    }
    
    /// <summary>
    /// Creates the sample holder.
    /// </summary>
    protected async Task CreateSampleHolder()
    {
        SampleHolder = new SampleHolder
        {
            DownloadUpdate = (sample, current, count) =>
            {
                Log($"({current} - {count}): Downloading: \'{sample}\'");
            }
        };

        await SampleHolder.LoadSampleList();
        SampleHolder.PrepareDirectory();
        await SampleHolder.DownloadSamples();
        await SampleHolder.DownloadImages();
        SampleHolder.LoadSamplesIntoMemory();
    }

    protected async Task<SampleHolder> GetSampleHolder()
    {
        if (SampleHolder == null) await CreateSampleHolder();
        return SampleHolder!;
    }
    
    /// <summary>
    /// This method updates the current sequence.
    /// </summary>
    /// <param name="location">The location of the sequence you want to use.</param>
    public virtual async Task UpdateSequence(string location)
    {
        var file_data = await File.ReadAllTextAsync(location);
        var sequence = Sequence.FromString(file_data);
        await UpdateSequence(sequence);
        _sequence_location = location;
    }
    
    /// <summary>
    /// This method updates the current sequence.
    /// </summary>
    /// <param name="sequence">The sequence you want to use.</param>
    public virtual async Task UpdateSequence(Sequence sequence)
    {
        const int update_rate = 100_000;
        _sequence_location = null;

        await SequencePlayer.Stop();

        var calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = update_rate,
            AddVisualEvents = true
        });

        var placement = calculator.Calculate(sequence).ToArray();
        TimedEvents.TimingSampleRate = update_rate;
        TimedEvents.Placement = placement;
        TimedEvents.Sequence = sequence;
        
        var sample_holder = await GetSampleHolder().ConfigureAwait(true);
        var buffer_holder = new BufferHolder();

        var audio_context = SequencePlayer.GetContext();
        var pcm_encoder = new PcmEncoder(sample_holder, new EncoderSettings
        {
            SampleRate = (uint) audio_context.SampleRate,
            Channels = 2,
            Resampler = new LinearResampler()
        });

        var (samples, _) = await pcm_encoder.GetAudioSamples(-1, placement);
        
        foreach (var ev in samples)
        {
            var value = ev.Value;
            var name = ev.Name;

            if (buffer_holder.ProcessedBuffers.TryGetValue(name, out var value_dictionary))
            {
                if (value_dictionary.ContainsKey(value)) continue;
            }

            if (value_dictionary == null)
            {
                value_dictionary = new Dictionary<double, AudibleBuffer>();
                buffer_holder.ProcessedBuffers.Add(name, value_dictionary);
            }

            var sample = audio_context.GetBufferObject(ev.AudioData, audio_context.SampleRate);
            value_dictionary.Add(value, sample);
        }
        
        await SequencePlayer.UpdateSequence(buffer_holder, TimedEvents);
        SequencePlayer.ClearSubscriptions();
        SetSequencePlayerSubscriptions(SequencePlayer);
        
        HandleAfterSequenceUpdate(TimedEvents);
        await SequencePlayer.Start();
    }

    /// <summary>
    /// Called after the sequence has finished loading.
    /// </summary>
    /// <param name="events">The events the sequence contains.</param>
    protected abstract void HandleAfterSequenceUpdate(TimedEvents events);
    
    /// <summary>
    /// Called by the abstract class in order to use the implementation, when the SequencePlayer is created.
    /// </summary>
    /// <param name="player">The created SequencePlayer.</param>
    protected abstract void SetSequencePlayerSubscriptions(SequencePlayer player);

    /// <summary>
    /// Call this when you want to check if the sequence is updated and you want to update it.
    /// </summary>
    protected virtual void HandleIfSequenceUpdate()
    {
        if (_sequence_location == null) return;
        
    }
}
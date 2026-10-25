using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Encoder.Wave;
using ThirtyDollarConverter.Legacy;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using ThirtyDollarConverter.Voices;

namespace ThirtyDollarConverter;

// The legacy renderer is obsolete; this class is its one deliberate dispatcher.
#pragma warning disable CS0618

public class PcmEncoder
{
    private readonly uint _channels;
    private readonly SemaphoreSlim _indexLock = new(1);
    private readonly SampleProcessor _sampleProcessor;
    private readonly uint _sampleRate;
    private readonly EncoderSettings _settings;
    private readonly VoiceList.Builder _voiceBuilder;

    /// <summary>
    ///     Creates a TDW sequence encoder.
    /// </summary>
    /// <param name="samples">The sample holder that stores the sequence's samples.</param>
    /// <param name="settings">The encoder's settings.</param>
    /// <param name="loggerAction">Action that handles log messages.</param>
    /// <param name="indexReport">Action that receives encode progress.</param>
    /// <exception cref="Exception">Exception that is thrown when the amount of channels is invalid.</exception>
    public PcmEncoder(SampleHolder samples, EncoderSettings settings,
        Action<string>? loggerAction = null,
        Action<ulong, ulong>? indexReport = null)
    {
        _settings = settings;
        Holder = samples;
        _channels = settings.Channels;
        _sampleRate = settings.SampleRate;

        Log = loggerAction ?? (_ => { });
        IndexReport = indexReport ?? ((_, _) => { });
        _sampleProcessor = new SampleProcessor(Holder.SampleList, settings, Log);
        PlacementCalculator = new PlacementCalculator(settings, Log);
        Legacy = new LegacyTrackRenderer(this, Holder, settings, Log);
        _voiceBuilder = new VoiceList.Builder(Holder, settings);

        switch (_channels)
        {
            case < 1:
                throw new Exception("Having less than one channel is literally impossible.");
            case > 2:
                throw new Exception("Having more than two audio channels isn't currently supported.");
        }
    }

    private SampleHolder Holder { get; }
    private Action<string> Log { get; }
    private Action<ulong, ulong> IndexReport { get; }
    internal PlacementCalculator PlacementCalculator { get; }

    /// <summary>The renderer <see cref="RenderMethod.LegacyTracks" /> dispatches to.</summary>
    private LegacyTrackRenderer Legacy { get; }


    /// <summary>
    ///     This method starts the encoding process for multiple sequences to be combined.
    /// </summary>
    /// <param name="sequences">The sequences you want to encode.</param>
    /// <param name="existingProcessedEvents">
    ///     Already resampled sounds from a previous render to reuse, so a re-render only
    ///     resamples the sounds an edit actually changed. Unused entries are pruned, so the
    ///     cache can't grow without bound.
    /// </param>
    /// <returns>A RenderedSequence object that stores the encoded audio and metadata.</returns>
    public async Task<RenderedSequence> GetMultipleSequencesAudio(IEnumerable<Sequence> sequences,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        var array = sequences as Sequence[] ?? [.. sequences];
        if (_settings.Renderer == RenderMethod.LegacyTracks)
            return await Legacy.RenderSequences(array, existingProcessedEvents);

        using var render = await BuildVoices(array, existingProcessedEvents);
        Log("Built the voice list. Starting audio mixing.");

        return new RenderedSequence
        {
            TimedEvents = render.TimedEvents,
            Audio = VoiceMixer.MixAll(render.Voices),
            AudioSampleRate = _sampleRate,
            ProcessedEvents = render.ProcessedEvents
        };
    }

    /// <summary>
    ///     This method starts the encoding process.
    /// </summary>
    /// <param name="sequence">The sequence you want to encode.</param>
    /// <returns>A RenderedSequence object that stores the encoded audio and metadata.</returns>
    public async Task<RenderedSequence> GetSequenceAudio(Sequence sequence)
    {
        return await GetMultipleSequencesAudio([sequence]);
    }

    /// <summary>
    ///     Places and resamples <paramref name="sequences" /> and builds their voices, without
    ///     mixing them - for playback, which streams the voices a block at a time. The caller owns
    ///     the returned render and disposes it.
    /// </summary>
    /// <param name="sequences">The sequences to place.</param>
    /// <param name="existingProcessedEvents">The resample cache to reuse and prune, as in <see cref="GetMultipleSequencesAudio" />.</param>
    public async Task<VoiceRender> BuildVoices(IEnumerable<Sequence> sequences,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        var (timed_events, processed_events) =
            await Prepare(sequences as Sequence[] ?? [.. sequences], existingProcessedEvents);
        return NewVoiceRender(timed_events, processed_events);
    }

    /// <summary>
    ///     <see cref="BuildVoices(IEnumerable{Sequence}, Dictionary{ValueTuple{string, double}, ProcessedEvent}?)" />
    ///     for a song the caller has already placed, so it isn't placed a second time. Placements
    ///     that aren't <see cref="Placement.Audible" /> - the visualizer's visual events - are
    ///     skipped, so a placement made with <see cref="EncoderSettings.AddVisualEvents" /> builds
    ///     the same voices as one made without. The render keeps <paramref name="timedEvents" />.
    /// </summary>
    public async Task<VoiceRender> BuildVoices(TimedEvents timedEvents,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        if (timedEvents.TimingSampleRate != _sampleRate)
            throw new ArgumentException(
                $"Placed at {timedEvents.TimingSampleRate} Hz, but this encoder renders at {_sampleRate} Hz.",
                nameof(timedEvents));

        var processed_events = await Resample(timedEvents, existingProcessedEvents);
        return NewVoiceRender(timedEvents, processed_events);
    }

    private VoiceRender NewVoiceRender(TimedEvents timedEvents,
        Dictionary<(string, double), ProcessedEvent> processedEvents)
    {
        return new VoiceRender
        {
            TimedEvents = timedEvents,
            Voices = BuildVoiceList(timedEvents, processedEvents),
            ProcessedEvents = processedEvents,
            SampleRate = _sampleRate
        };
    }

    /// <summary>
    ///     The legacy track renderer's incremental update - see
    ///     <see cref="LegacyTrackRenderer.ComputeIncrementalAudio" />. Always renders with
    ///     <see cref="RenderMethod.LegacyTracks" />, whatever <see cref="EncoderSettings.Renderer" />
    ///     says: a voice render has no tracks to update, and rebuilding it is the update.
    /// </summary>
    public async Task<RenderedSequence> ComputeIncrementalAudio(RenderedSequence oldRendered,
        IEnumerable<Sequence> newSequences)
    {
        return await Legacy.ComputeIncrementalAudio(oldRendered, newSequences);
    }

    public async Task<AudioData<float>> GetAudioFromTimedEvents(TimedEvents timedEvents,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        Log("Calculated placement. Starting sample processing.");

        var processed_events = await GetAudioSamples(timedEvents, existingProcessedEvents);

        Log("Finished processing all samples. Starting audio mixing.");
        if (_settings.Renderer == RenderMethod.LegacyTracks)
            return (await Legacy.GenerateAudioAndMixer(timedEvents, processed_events)).Item1;

        using var voices = BuildVoiceList(timedEvents, processed_events);
        return VoiceMixer.MixAll(voices);
    }

    /// <summary>
    ///     Placement, then resampling against <paramref name="existingProcessedEvents" />, pruning
    ///     it of the sounds the new placement no longer uses.
    /// </summary>
    internal async Task<(TimedEvents, Dictionary<(string, double), ProcessedEvent>)> Prepare(Sequence[] sequences,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents)
    {
        var placement = PlacementCalculator.CalculateMany(sequences);
        var placement_array = placement.ToArray();

        var timed_events = new TimedEvents
        {
            Sequences = sequences,
            Placement = placement_array,
            TimingSampleRate = (int)_sampleRate
        };

        return (timed_events, await Resample(timed_events, existingProcessedEvents));
    }

    /// <summary>Resampling against <paramref name="existingProcessedEvents" />, pruned as in <see cref="Prepare" />.</summary>
    private async Task<Dictionary<(string, double), ProcessedEvent>> Resample(TimedEvents timedEvents,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents)
    {
        Log("Calculated placement. Starting sample processing.");
        var processed_events = await GetAudioSamples(timedEvents, existingProcessedEvents);
        if (existingProcessedEvents != null) RemoveUnusedAudioSamples(processed_events, timedEvents);

        return processed_events;
    }

    private VoiceList BuildVoiceList(TimedEvents events, Dictionary<(string, double), ProcessedEvent> processed)
    {
        // one builder per encoder, and an export can run beside a playback rebuild
        lock (_voiceBuilder)
        {
            return _voiceBuilder.Build(events, processed);
        }
    }

    /// <summary>
    ///     This method gets all resampled audio samples.
    /// </summary>
    /// <param name="events">The calculated events.</param>
    /// <param name="existingProcessedEvents">
    ///     A dictionary containing already processed events from a possible previous
    ///     conversion.
    /// </param>
    /// <returns>An array containing all processed events.</returns>
    /// <exception cref="Exception">Edge case that only can happen if something is wrong with the program.</exception>
    public async Task<Dictionary<(string, double), ProcessedEvent>> GetAudioSamples(TimedEvents events,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        var placement = events.Placement;
        // Get only unique events. Duplicates get removed.
        var to_process_dictionary = new Dictionary<(string event_name, double event_value), BaseEvent>();
        var processed_events_dictionary = existingProcessedEvents ?? new Dictionary<(string, double), ProcessedEvent>();

        foreach (var p in placement)
        {
            if (!p.Audible) continue;

            var ev = p.Event;
            var event_name = ev.SoundEvent ?? string.Empty;
            var event_value = ev.Value;
            if (event_name == "!cut" || ev is ICustomActionEvent) continue;

            if (Holder.StringToSoundReferences.TryGetValue(event_name, out var sound))
                event_name = sound.Id;

            var key = (event_name, event_value);
            if (processed_events_dictionary.ContainsKey(key)) continue;
            to_process_dictionary.TryAdd(key, ev);
        }

        // Start setting up tasks to process all events.
        if (to_process_dictionary.Count == 0) return processed_events_dictionary;

        var todo_samples = to_process_dictionary.Values.ToArray();
        var processed_events = new ProcessedEvent[todo_samples.Length];
        var processed_events_memory = processed_events.AsMemory();

        for (var i = 0; i < processed_events.Length; i++)
        {
            var current_event = todo_samples[i];

            var processed_event = new ProcessedEvent(current_event);
            processed_events_memory.Span[i] = processed_event;
        }

        ulong finished_tasks = 0;
        var total_tasks = (ulong)processed_events.Length;

        // Distribute sample processing to different threads.
        var task = Parallel.ForEachAsync(processed_events, async (processedEvent, token) =>
        {
            processedEvent.ProcessAudioData(_sampleProcessor);
            await _indexLock.WaitAsync(token);

            finished_tasks++;
            IndexReport(finished_tasks, total_tasks);

            _indexLock.Release();
        });

        await task;

        var idx = 0;
        foreach (var tuple in to_process_dictionary.Keys)
        {
            processed_events_dictionary.Add(tuple, processed_events_memory.Span[idx]);
            idx++;
        }

        return processed_events_dictionary;
    }

    /// <summary>
    ///     Removes all events from the dictionary that are not present in the used keys set.
    /// </summary>
    /// <param name="processedEvents">The dictionary of processed events.</param>
    /// <param name="events">The timed events to build the used keys from.</param>
    internal void RemoveUnusedAudioSamples(Dictionary<(string, double), ProcessedEvent> processedEvents,
        TimedEvents events)
    {
        var usedKeys = new HashSet<(string, double)>();
        foreach (var p in events.Placement)
        {
            if (!p.Audible) continue;

            var ev = p.Event;
            var eventName = ev.SoundEvent ?? string.Empty;
            var eventValue = ev.Value;
            if (eventName == "!cut" || ev is ICustomActionEvent) continue;

            if (Holder.StringToSoundReferences.TryGetValue(eventName, out var sound))
                eventName = sound.Id;

            usedKeys.Add((eventName, eventValue));
        }

        foreach (var key in processedEvents.Keys.Where(key => !usedKeys.Contains(key)).ToList())
            processedEvents.Remove(key);
    }

    /// <summary>
    ///     Exports an AudioData object as a WAVE file.
    /// </summary>
    /// <param name="location">The location you want to export to.</param>
    /// <param name="data">The AudioData object.</param>
    public void WriteAsWavFile(string location, AudioData<float> data)
    {
        WaveEncoder.WriteAsWavFloat32File(location, data, _channels, _sampleRate, _settings.EnableNormalization,
            IndexReport);
        Log("Saved audio file.");
    }

    public void WriteAsWavFile(Stream stream, AudioData<float> data)
    {
        WaveEncoder.WriteAsWavFloat32File(stream, data, _channels, _sampleRate, _settings.EnableNormalization,
            IndexReport);
        Log("Saved audio file.");
    }
}

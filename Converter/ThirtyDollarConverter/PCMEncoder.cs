using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThirtyDollarConverter.Encoder.Mixers;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Encoder.Wave;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter;

public class PcmEncoder
{
    private readonly uint _channels;
    private readonly SemaphoreSlim _indexLock = new(1);
    private readonly SampleProcessor _sampleProcessor;
    private readonly uint _sampleRate;
    private readonly EncoderSettings _settings;

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
    private PlacementCalculator PlacementCalculator { get; }

    /// <summary>
    ///     This method starts the encoding process for multiple sequences to be combined.
    /// </summary>
    /// <param name="sequences">The sequences you want to encode.</param>
    /// <param name="existingProcessedEvents">
    ///     Already resampled sounds from a previous render to reuse. Resampling dominates a
    ///     cold render when a project uses many distinct pitches, and a full re-render of an
    ///     edited project reuses nearly all of them - only the edited sound is ever new.
    ///     Unused entries are pruned so the cache can't grow without bound.
    /// </param>
    /// <returns>A RenderedSequence object that stores the encoded audio and metadata.</returns>
    public async Task<RenderedSequence> GetMultipleSequencesAudio(IEnumerable<Sequence> sequences,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        var array = sequences as Sequence[] ?? [.. sequences];
        var placement = PlacementCalculator.CalculateMany(array);
        var placement_array = placement.ToArray();

        var timed_events = new TimedEvents
        {
            Sequences = array,
            Placement = placement_array,
            TimingSampleRate = (int)_sampleRate
        };

        Log("Calculated placement. Starting sample processing.");
        var processed_events = await GetAudioSamples(timed_events, existingProcessedEvents);
        if (existingProcessedEvents != null) RemoveUnusedAudioSamples(processed_events, timed_events);

        Log("Finished processing all samples. Starting audio mixing.");
        var (audio, mixer) = await GenerateAudioAndMixer(timed_events, processed_events);

        return new RenderedSequence
        {
            TimedEvents = timed_events,
            Audio = audio,
            AudioSampleRate = _sampleRate,
            Mixer = mixer,
            ProcessedEvents = processed_events
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
    ///     Re-renders <paramref name="oldRendered" /> against a new set of sequences, re-using its
    ///     mixer and processed samples: it works out which sample range the edit can possibly be
    ///     audible in, wipes that range in every track, and re-renders it from the full new
    ///     placement list.
    ///     <para>
    ///         That range is <c>[first changed placement, last changed placement + biggestEventLength)</c>:
    ///         a sound reaches at most <c>biggestEventLength</c> past where it starts, and a cut can
    ///         only silence sounds that started before it - so no edit is audible more than one
    ///         biggest-sample beyond the last placement it changed. <see cref="ProcessChunk" /> already
    ///         looks that far back when picking the placements for a slice, so replaying the range
    ///         reproduces exactly what a full render would put there, cuts included - which is why a
    ///         cut in the diff is an ordinary edit here rather than a reason to re-render everything.
    ///     </para>
    ///     Falls back to a full render when the render's length changes, since that moves the chunk
    ///     grid the untouched audio was rendered against (see <see cref="ChunkBoundaries" />).
    /// </summary>
    /// <fun-fact>
    ///     I burnt 7 dollars worth of AI credits to try to write this method and ended up writing it myself anyway.
    ///     <i>(AI is going to overtake us oooohhhh scary...)</i> (23 more left :D)
    /// </fun-fact>
    public async Task<RenderedSequence> ComputeIncrementalAudio(RenderedSequence oldRendered,
        IEnumerable<Sequence> newSequences)
    {
        if (oldRendered.Mixer == null || oldRendered.ProcessedEvents == null)
            return await GetMultipleSequencesAudio(newSequences, oldRendered.ProcessedEvents);

        var new_sequences = newSequences as Sequence[] ?? [.. newSequences];
        var new_placements = PlacementCalculator.CalculateMany(new_sequences).ToArray();

        var final_timed_events = new TimedEvents
        {
            Sequences = new_sequences,
            Placement = new_placements,
            TimingSampleRate = (int)_sampleRate
        };

        // extract differences, counting duplicate placements separately
        var to_remove = MultisetDifference(oldRendered.Placement, new_placements);
        var to_add = MultisetDifference(new_placements, oldRendered.Placement);

        // nothing audible changed, keep the old audio
        if (to_remove.Length == 0 && to_add.Length == 0)
        {
            oldRendered.TimedEvents = final_timed_events;
            return oldRendered;
        }

        // A sound that lives on a track the old mixer never allocated would be re-rendered onto
        // the default track instead, and cuts targeting it would miss. Only a full render
        // creates tracks.
        foreach (var sequence in new_sequences)
        foreach (var channel in sequence.SeparatedChannels)
        {
            var channel_id = Holder.StringToSoundReferences.TryGetValue(channel, out var sound)
                ? sound.Id
                : channel;
            if (!oldRendered.Mixer.HasTrack(channel_id))
                return await GetMultipleSequencesAudio(new_sequences, oldRendered.ProcessedEvents);
        }

        var final_sounds = await GetAudioSamples(final_timed_events, oldRendered.ProcessedEvents);

        // Prune before measuring, not after: a full render sizes itself off the samples its own
        // placements use, so an edit that drops the longest sound has to shorten the render with
        // it - otherwise the length check below sees no change and keeps a buffer a full render
        // would never have produced. Nothing here reads the old samples, so this is safe to do
        // up front (the subtract-based renderer this replaced couldn't - it needed them to
        // subtract with).
        RemoveUnusedAudioSamples(final_sounds, final_timed_events);

        var big_event = final_sounds.Values.MaxBy(e => e.AudioData.GetLength());
        var big_event_length = big_event?.AudioData.GetLength() ?? 0;

        var mixer = oldRendered.Mixer;
        var length = new_placements.Length > 0 ? (int)new_placements[^1].Index + big_event_length : 0;
        if (length != mixer.GetLength())
            return await GetMultipleSequencesAudio(new_sequences, oldRendered.ProcessedEvents);

        var dirty_start = int.MaxValue;
        var dirty_end = 0;
        foreach (var placement in to_remove.Concat(to_add))
        {
            dirty_start = Math.Min(dirty_start, (int)placement.Index);
            dirty_end = Math.Max(dirty_end, (int)placement.Index + big_event_length);
        }

        var (start, end) = SnapToChunks(length, Math.Max(dirty_start, 0), Math.Min(dirty_end, length));

        if (end > start)
        {
            foreach (var (_, track) in mixer.GetTracks())
                for (var channel = 0; channel < track.Samples.Length; channel++)
                    track.GetChannel(channel).AsSpan()[start..end].Clear();

            await RenderChunkRange(mixer, final_timed_events, final_sounds, big_event_length, start, end);
        }

        oldRendered.ProcessedEvents = final_sounds;
        oldRendered.AudioSampleRate = _sampleRate;
        oldRendered.Audio = mixer.MixDown();
        oldRendered.Mixer = mixer;
        oldRendered.TimedEvents = final_timed_events;
        return oldRendered;
    }

    public async Task<AudioData<float>> GetAudioFromTimedEvents(TimedEvents timedEvents,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        var (audio, _) = await GetAudioAndMixerFromTimedEvents(timedEvents, existingProcessedEvents);
        return audio;
    }

    public async Task<(AudioData<float>, AudioMixer)> GetAudioAndMixerFromTimedEvents(TimedEvents timedEvents,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        Log("Calculated placement. Starting sample processing.");

        var processed_events = await GetAudioSamples(timedEvents, existingProcessedEvents);

        Log("Finished processing all samples. Starting audio mixing.");
        return await GenerateAudioAndMixer(timedEvents, processed_events);
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
    private void RemoveUnusedAudioSamples(Dictionary<(string, double), ProcessedEvent> processedEvents,
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
    ///     This method creates the final audio.
    /// </summary>
    /// <param name="events">The placement of each event.</param>
    /// <param name="processedEvents">The resampled sounds.</param>
    /// <param name="cancellationToken">A token that cancels the waiting task.</param>
    /// <returns>An AudioData object that stores the encoded audio.</returns>
    private async Task<(AudioData<float>, AudioMixer)> GenerateAudioAndMixer(TimedEvents events,
        Dictionary<(string, double), ProcessedEvent> processedEvents,
        CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;
        var last_placement = events.Placement[^1];

        var big_event = processedEvents.Values.MaxBy(e => e.AudioData.GetLength());
        var big_event_length = big_event?.AudioData.GetLength() ?? 0;
        var length = (int)last_placement.Index + big_event_length;
        var audio_data = AudioData<float>.WithLength(_channels, length);

        var mixer = new AudioMixer(audio_data);
        foreach (var sequence in events.Sequences)
        foreach (var channel in sequence.SeparatedChannels)
        {
            if (mixer.HasTrack(channel)) continue;
            var channelID = Holder.StringToSoundReferences.TryGetValue(channel, out var sound)
                ? sound.Id
                : channel;
            var new_track = AudioData<float>.WithLength(_channels, length);
            mixer.AddTrack(channelID, new_track);
        }

        // Map channel tasks.
        await RenderTimedEvents(mixer, events, processedEvents, big_event_length, token);

        return (mixer.MixDown(), mixer);
    }

    private async Task<AudioData<float>> GenerateAudioData(TimedEvents events,
        Dictionary<(string, double), ProcessedEvent> processedEvents,
        CancellationToken? cancellationToken = null)
    {
        var (audio, _) = await GenerateAudioAndMixer(events, processedEvents, cancellationToken);
        return audio;
    }

    /// <summary>
    ///     Adds events to an existing AudioMixer.
    /// </summary>
    /// <param name="mixer">The mixer to render into.</param>
    /// <param name="events">The events to render.</param>
    /// <param name="processedEvents">The processed audio samples.</param>
    /// <param name="biggestEventLength">The length of the biggest event in the sequence.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RenderTimedEvents(AudioMixer mixer, TimedEvents events,
        Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength,
        CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;

        // Map channel tasks.
        var channels = new Task[_channels];
        for (var i = 0; i < _channels; i++)
        {
            var index = i;

            channels[index] =
                Task.Run(
                    async () => { await ProcessChannel(mixer, index, events, processedEvents, biggestEventLength); },
                    token);
        }

        // Wait for all tasks to finish.
        await Task.WhenAll(channels);
    }

    /// <summary>
    ///     Renders <paramref name="events" /> into the chunks of the grid that intersect
    ///     <c>[<paramref name="rangeStart" />, <paramref name="rangeEnd" />)</c>, leaving the rest of the
    ///     mixer untouched. Used by <see cref="ComputeIncrementalAudio" /> to replay only the range
    ///     an edit can be heard in.
    ///     <para>
    ///         The wanted range is never handed to <see cref="ProcessChunk" /> as its slice, even though
    ///         it takes a start and an end: those bounds are the chunk grid a full render uses, and they
    ///         are audible. <see cref="SampleMixer.HandleCut" />'s search for silence stops at the end of the slice it
    ///         is given, so a cut rendered inside one wide slice - or inside a slice that starts partway
    ///         through a chunk - zeroes a different number of samples than the same cut in a full render.
    ///         Re-rendering whole chunks of the same grid instead keeps the output bit-identical, which is
    ///         the property the incremental path is built on.
    ///     </para>
    /// </summary>
    /// <param name="mixer">The mixer to render into. Its range must already be cleared.</param>
    /// <param name="events">The full placement list, not just the changed placements.</param>
    /// <param name="processedEvents">The processed audio samples.</param>
    /// <param name="biggestEventLength">The length of the biggest event in the sequence.</param>
    /// <param name="rangeStart">First sample to re-render, rounded down to a chunk boundary.</param>
    /// <param name="rangeEnd">Sample to re-render up to, exclusive, rounded up to a chunk boundary.</param>
    private async Task RenderChunkRange(AudioMixer mixer, TimedEvents events,
        Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength,
        int rangeStart, int rangeEnd)
    {
        var boundaries = ChunkBoundaries(mixer.GetLength());

        // every (channel, chunk) pair writes to its own disjoint span, so they can all run at once
        var work =
            from channel in Enumerable.Range(0, (int)_channels)
            from i in Enumerable.Range(1, boundaries.Length - 1)
            where boundaries[i - 1] < rangeEnd && boundaries[i] > rangeStart && boundaries[i - 1] < boundaries[i]
            select (channel, chunk: i);

        await Parallel.ForEachAsync(work, (item, _) =>
        {
            ProcessChunk(boundaries[item.chunk - 1], boundaries[item.chunk], mixer, item.channel, events,
                processedEvents, biggestEventLength);
            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    ///     The sample offsets a render of <paramref name="length" /> samples is split into for
    ///     multithreading: chunk <c>i</c> covers <c>[result[i], result[i + 1])</c>.
    ///     <see cref="ComputeIncrementalAudio" /> re-renders whole chunks of this exact
    ///     grid rather than an arbitrary sample range, because a chunk boundary is observable in
    ///     the output: <see cref="SampleMixer.HandleCut" />'s search for silence restarts at one, so a cut
    ///     rendered against different boundaries can stop zeroing somewhere else.
    /// </summary>
    private int[] ChunkBoundaries(int length)
    {
        var min_length_per_thread = Math.Min(1 << 15, length);
        var working_threads = _settings.MultithreadingSlices;

        var min_length_for_working_threads = min_length_per_thread * working_threads;
        while (min_length_for_working_threads > length && working_threads > 1)
            min_length_for_working_threads = min_length_per_thread * --working_threads;

        var ratio = (float)length / min_length_for_working_threads;
        if (ratio < 1)
            throw new Exception($"Invalid calculation of separated threads.\n" +
                                $"Length: {length}, MinLengthThreads: {min_length_for_working_threads}, " +
                                $"MinLengthForThread: {min_length_per_thread}, Ratio: {ratio}");

        var chunk_size = length / (float)working_threads;
        var boundaries = new int[working_threads + 1];
        for (var i = 0; i < working_threads; i++) boundaries[i] = Math.Min((int)(i * chunk_size), length);
        boundaries[working_threads] = length;

        return boundaries;
    }

    /// <summary>
    ///     The span the chunk grid actually covers for a wanted range - the wanted range
    ///     grown outwards to the nearest chunk boundaries. Empty (start == end) when nothing
    ///     intersects.
    /// </summary>
    internal (int Start, int End) SnapToChunks(int length, int rangeStart, int rangeEnd)
    {
        if (length <= 0) return (0, 0);

        var boundaries = ChunkBoundaries(length);
        var start = length;
        var end = 0;

        for (var i = 1; i < boundaries.Length; i++)
        {
            if (boundaries[i - 1] >= rangeEnd || boundaries[i] <= rangeStart) continue;
            start = Math.Min(start, boundaries[i - 1]);
            end = Math.Max(end, boundaries[i]);
        }

        return end <= start ? (0, 0) : (start, end);
    }

    /// <summary>
    ///     Processes an export channel.
    /// </summary>
    /// <param name="mixer">The channel you want to work on.</param>
    /// <param name="channel">The channel ID.</param>
    /// <param name="events">The calculated events.</param>
    /// <param name="processedEvents">The processed events for the sequence.</param>
    /// <param name="biggestEventLength">The sequence's biggest event's length.</param>
    private async Task ProcessChannel(AudioMixer mixer, int channel, TimedEvents events,
        Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength)
    {
        var length = mixer.GetLength();
        var boundaries = ChunkBoundaries(length);

        await Parallel.ForAsync(1, boundaries.Length, (i, _) =>
        {
            var start = boundaries[i - 1];
            var end = boundaries[i];

            if (start >= end) return ValueTask.CompletedTask;

            var start_time = Stopwatch.GetTimestamp();
            ProcessChunk(start, end, mixer, channel, events, processedEvents, biggestEventLength);
            var delta = Stopwatch.GetElapsedTime(start_time);
            Log(
                $@"Processed chunk i: {i} in {delta:ss\.ffff} s. Start: {start}, End: {end}, Length: {length}");

            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    ///     Renders every placement audible in <c>[<paramref name="start" />, <paramref name="end" />)</c>
    ///     into that slice of the mixer. The bounds are a chunk of the grid
    ///     <see cref="ChunkBoundaries" /> lays out, and are audible in their own right - see the note
    ///     on <see cref="RenderChunkRange" /> before calling with anything else.
    /// </summary>
    private void ProcessChunk(int start, int end, AudioMixer mixer, int channel,
        TimedEvents events, Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength)
    {
        var placement = events.Placement.AsSpan();

        foreach (var current in placement)
        {
            // skip non-audible
            if (!current.Audible) continue;

            // get current event start.
            var current_start = (int)current.Index;
            if (current_start < start - biggestEventLength) continue;
            if (current_start >= end) break;

            RenderEventToSlice(start, end, mixer, channel, current, processedEvents);
        }
    }

    private void RenderEventToSlice(int start, int end, AudioMixer mixer, int channel,
        Placement current, Dictionary<(string, double), ProcessedEvent> processedEvents)
    {
        // extract event values
        var current_event = current.Event;
        var (event_name, event_value, event_volume) = current.Event;
        event_name ??= string.Empty;

        if (Holder.StringToSoundReferences.TryGetValue(event_name, out var sound_reference))
            event_name = sound_reference.Id;

        // get specified event track if exists
        var track_data = mixer.GetTrackOrDefault(event_name);
        var channel_data = track_data.GetChannel(channel).AsSpan();

        // slice only memory which will be worked on
        var mix_slice = channel_data[start..end];

        // get current event start.
        var current_start = (int)current.Index;

        // put extended variables here to be used later
        var pan = 0f;
        var startOffset = 0d;

        switch (current_event)
        {
            // handle #icut event
            case IndividualCutEvent individual_cut_event:
            {
                foreach (var cut_track in individual_cut_event.CutSounds
                             .Select(sound => Holder.StringToSoundReferences.TryGetValue(sound, out var reference)
                                 ? reference.Id
                                 : sound)
                             .Where(sound => mixer.HasTrack(sound))
                             .Select(sound => mixer.GetTrack(sound)))
                {
                    var cut_slice = cut_track.GetChannel(channel).AsSpan()[start..end];
                    SampleMixer.HandleCut(start, end, current_start, cut_slice, _sampleRate,
                        _settings.CutFadeLengthMs);
                }

                return;
            }

            case ExtendedEvent extended_event:
            {
                pan = Math.Clamp(extended_event.Pan, -100f, 100f);
                startOffset = Math.Max(extended_event.OffsetInSeconds, 0);
                break;
            }
        }

        // handle !cut event
        if (event_name == "!cut")
        {
            foreach (var (_, data) in mixer.GetTracks())
                SampleMixer.HandleCut(start, end, current_start, data.GetChannel(channel).AsSpan()[start..end],
                    _sampleRate, _settings.CutFadeLengthMs);

            return;
        }

        // search for processed sample
        if (!processedEvents.TryGetValue((event_name, event_value), out var processed_event))
        {
            Log($"Event {event_name} with value {event_value} not found in processed events");
            return;
        }

        // get its length
        var current_length = processed_event.AudioData.GetLength();
        // get the channel that the mixer is also on
        var current_channel = processed_event.AudioData.GetChannel(channel);

        // case: event not valid for current mixer slice
        if (current_start + current_length < start) return;

        // normalize values to current mixer slice
        var delta_start = current_start - start;
        var delta_end = current_length;

        var offset = 0;
        if (delta_start < 0)
        {
            offset = -delta_start;
            delta_start = 0;
        }

        delta_end -= offset;

        // the current sample rate is calculated using (uint)(_sampleRate / Math.Pow(2, ev.Value / 12))
        if (startOffset > 0)
        {
            var event_sample_rate = _sampleRate / Math.Pow(2, event_value / 12);
            var offsetInSamples = (int)(startOffset * event_sample_rate);
            if (offsetInSamples > current_length) offsetInSamples = current_length;

            offset += offsetInSamples;
            delta_end -= offsetInSamples;
        }

        if (delta_end <= 0) return;

        if (delta_end + delta_start >= mix_slice.Length) delta_end = mix_slice.Length - delta_start;

        var volume = event_volume;

        if (pan != 0 && _settings.PanScale == PercentageScale.EqualPower)
        {
            RenderEqualPowerPan(processed_event, channel, pan, volume, current_channel, mix_slice,
                delta_start, delta_end, offset);
            return;
        }

        // when panning is used, subtracts the channel opposite to what the value represents
        // if it's -1 (left channel only), subtracts the right channel and vice versa
        switch (pan)
        {
            // Channel = Right
            case < 0 when channel == 1:
            {
                var percent_subtract = 1f + pan / 100f;
                volume *= _settings.PanScale switch
                {
                    PercentageScale.Logarithmic => MathF.Sqrt(percent_subtract),
                    PercentageScale.LinearOverflowLogarithmic or PercentageScale.Linear => percent_subtract,
                    _ => 0
                };
                break;
            }

            // Channel = Left
            case > 0 when channel == 0:
            {
                var percent_subtract = 1f - pan / 100f;
                volume *= _settings.PanScale switch
                {
                    PercentageScale.Logarithmic => MathF.Sqrt(percent_subtract),
                    PercentageScale.LinearOverflowLogarithmic or PercentageScale.Linear => percent_subtract,
                    _ => 0
                };
                break;
            }
        }

        SampleMixer.RenderSample(current_channel, mix_slice, delta_start,
            volume, _settings.VolumeScale, delta_end, offset);
    }

    /// <summary>
    ///     Applies panning using the same equal-power curve as the Web Audio API's StereoPannerNode,
    ///     which the Thirty Dollar Website relies on. Mono and stereo sources use different formulas:
    ///     a mono source is split into L/R, while a stereo source gets the opposite channel downmixed
    ///     into the panned-to side instead of just attenuated (a well-known StereoPannerNode quirk).
    /// </summary>
    private void RenderEqualPowerPan(ProcessedEvent processed_event, int channel, float pan, double volume,
        Span<float> own_channel, Span<float> mix_slice, int delta_start, int delta_end, int offset)
    {
        var x = pan / 100f;
        var stereo_source = processed_event.AudioData.SourceChannels >= 2 && _settings.Channels >= 2;

        if (!stereo_source)
        {
            var angle = (x + 1f) * MathF.PI / 4f;
            var gain = channel == 0 ? MathF.Cos(angle) : MathF.Sin(angle);
            SampleMixer.RenderSample(own_channel, mix_slice, delta_start, volume * gain, _settings.VolumeScale,
                delta_end,
                offset);
            return;
        }

        var half_angle = (x <= 0 ? x + 1f : x) * MathF.PI / 2f;
        var cos = MathF.Cos(half_angle);
        var sin = MathF.Sin(half_angle);

        if (x <= 0)
        {
            var own_gain = channel == 0 ? 1f : sin;
            SampleMixer.RenderSample(own_channel, mix_slice, delta_start, volume * own_gain, _settings.VolumeScale,
                delta_end,
                offset);

            if (channel != 0) return;
            var right_channel = processed_event.AudioData.GetChannel(1);
            SampleMixer.RenderSample(right_channel, mix_slice, delta_start, volume * cos, _settings.VolumeScale,
                delta_end,
                offset);
        }
        else
        {
            var own_gain = channel == 1 ? 1f : cos;
            SampleMixer.RenderSample(own_channel, mix_slice, delta_start, volume * own_gain, _settings.VolumeScale,
                delta_end,
                offset);

            if (channel != 1) return;
            var left_channel = processed_event.AudioData.GetChannel(0);
            SampleMixer.RenderSample(left_channel, mix_slice, delta_start, volume * sin, _settings.VolumeScale,
                delta_end,
                offset);
        }
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

    /// <summary>
    ///     Returns the placements in <paramref name="from" /> that are not in <paramref name="subtract" />.
    ///     Unlike <see cref="Enumerable.Except{T}(IEnumerable{T},IEnumerable{T})" />, equal placements
    ///     are counted separately, so removing one of two identical stacked sounds is detected.
    /// </summary>
    private static Placement[] MultisetDifference(Placement[] from, Placement[] subtract)
    {
        var counts = new Dictionary<Placement, int>(PlacementEqualityComparer.Instance);
        foreach (var placement in subtract)
            counts[placement] = counts.GetValueOrDefault(placement) + 1;

        var result = new List<Placement>();
        foreach (var placement in from)
            if (counts.TryGetValue(placement, out var count) && count > 0) counts[placement] = count - 1;
            else result.Add(placement);

        return [.. result];
    }

    private class PlacementEqualityComparer : IEqualityComparer<Placement>
    {
        public static readonly PlacementEqualityComparer Instance = new();

        public bool Equals(Placement? x, Placement? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.Equals(y);
        }

        public int GetHashCode(Placement obj)
        {
            return obj.GetHashCode();
        }
    }
}
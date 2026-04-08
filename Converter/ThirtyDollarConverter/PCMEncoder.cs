using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

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
    /// <returns>A RenderedSequence object that stores the encoded audio and metadata.</returns>
    public async Task<RenderedSequence> GetMultipleSequencesAudio(IEnumerable<Sequence> sequences)
    {
        var array = sequences as Sequence[] ?? sequences.ToArray();
        var placement = PlacementCalculator.CalculateMany(array);
        var placement_array = placement.ToArray();

        var timed_events = new TimedEvents
        {
            Sequences = array,
            Placement = placement_array,
            TimingSampleRate = (int)_sampleRate
        };

        var audio = await GetAudioFromTimedEvents(timed_events);
        return new RenderedSequence
        {
            TimedEvents = timed_events,
            Audio = audio,
            AudioSampleRate = _sampleRate
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

    public async Task<RenderedSequence> GetIncrementalAudio(RenderedSequence oldRendered, IEnumerable<Sequence> newSequences)
    {
        var array = newSequences as Sequence[] ?? newSequences.ToArray();
        var placement = PlacementCalculator.CalculateMany(array);
        var new_placement_array = placement.ToArray();

        var timed_events = new TimedEvents
        {
            Sequences = array,
            Placement = new_placement_array,
            TimingSampleRate = (int)_sampleRate
        };

        // Determine difference
        var old_placement = oldRendered.Placement;
        var to_remove = old_placement.Except(new_placement_array).ToArray();
        var to_add = new_placement_array.Except(old_placement).ToArray();

        var processed_events = await GetAudioSamples(timed_events, null);

        // We need an AudioMixer to perform the operations
        var last_placement = new_placement_array[^1];
        var big_event = processed_events.Values.MaxBy(e => e.AudioData.GetLength());
        var big_event_length = big_event?.AudioData.GetLength() ?? 0;
        var length = (int)last_placement.Index + big_event_length;

        // If the length changed, we might need to re-allocate or handle it.
        // For simplicity, we assume the user wants to update the existing audio in place if possible, 
        // but AudioData size is fixed.
        var audio_data = oldRendered.Audio;
        if (audio_data.GetLength() < length)
        {
            // Re-allocation case - this is tricky because we'd lose the old audio data.
            // If the sequence gets longer, we should probably resize the AudioData.
            // However, incremental updates usually imply staying within a similar range.
            // Let's assume we can create a new AudioData and copy the old one if needed, or just allocate enough.
            var new_audio_data = AudioData<float>.WithLength(_channels, length);
            for (var i = 0; i < _channels; i++)
            {
                audio_data.Samples[i].CopyTo(new_audio_data.Samples[i]);
            }
            audio_data = new_audio_data;
        }

        var mixer = new AudioMixer(audio_data);
        // Ensure tracks exist for new sounds
        foreach (var sequence in array)
        foreach (var channel in sequence.SeparatedChannels)
        {
            if (mixer.HasTrack(channel)) continue;
            var new_track = AudioData<float>.WithLength(_channels, audio_data.GetLength());
            mixer.AddTrack(channel, new_track);
        }

        // Subtract removed events
        if (to_remove.Length > 0)
        {
            var remove_timed = new TimedEvents { Placement = to_remove, Sequences = oldRendered.Sequences, TimingSampleRate = (int)_sampleRate };
            await RenderTimedEvents(mixer, remove_timed, processed_events, big_event_length, true);
        }

        // Add new events
        if (to_add.Length > 0)
        {
            var add_timed = new TimedEvents { Placement = to_add, Sequences = array, TimingSampleRate = (int)_sampleRate };
            await RenderTimedEvents(mixer, add_timed, processed_events, big_event_length, false);
        }

        oldRendered.Audio = mixer.MixDown();
        oldRendered.TimedEvents = timed_events;

        return oldRendered;
    }

    public async Task<AudioData<float>> GetAudioFromTimedEvents(TimedEvents timedEvents,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        Log("Calculated placement. Starting sample processing.");

        var processed_events = await GetAudioSamples(timedEvents, existingProcessedEvents);

        Log("Finished processing all samples. Starting audio mixing.");
        var audioData = await GenerateAudioData(timedEvents, processed_events);
        return audioData;
    }

    /// <summary>
    ///     This method gets all resampled audio samples.
    /// </summary>
    /// <param name="events">The calculated events.</param>
    /// <returns>An array containing all processed events.</returns>
    /// <exception cref="Exception">Edge case that only can happen if something is wrong with the program.</exception>
    public async Task<Dictionary<(string, double), ProcessedEvent>> GetAudioSamples(TimedEvents events,
        Dictionary<(string, double), ProcessedEvent>? existingProcessedEvents = null)
    {
        var placement = events.Placement;
        // Get only unique events. Duplicates get removed.
        var event_dictionary = new Dictionary<(string event_name, double event_value), BaseEvent>();
        var dictionary = existingProcessedEvents ?? new Dictionary<(string, double), ProcessedEvent>();

        foreach (var p in placement)
        {
            if (!p.Audible) continue;

            var ev = p.Event;
            var event_name = ev.SoundEvent ?? string.Empty;
            var event_value = ev.Value;
            if (event_name == "!cut" || ev is ICustomActionEvent) continue;

            if (Holder.StringToSoundReferences.TryGetValue(event_name, out var sound))
                event_name = sound.Id;

            if (dictionary.ContainsKey((event_name, event_value))) continue;
            event_dictionary.TryAdd((event_name, event_value), ev);
        }

        // Start setting up tasks to process all events.
        if (event_dictionary.Count == 0) return dictionary;
        var todo_samples = event_dictionary.Values.ToArray();
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
        foreach (var tuple in event_dictionary.Keys)
        {
            dictionary.Add(tuple, processed_events_memory.Span[idx]);
            idx++;
        }

        return dictionary;
    }

    /// <summary>
    ///     This method creates the final audio.
    /// </summary>
    /// <param name="events">The placement of each event.</param>
    /// <param name="processedEvents">The resampled sounds.</param>
    /// <param name="cancellationToken">A token that cancels the waiting task.</param>
    /// <returns>An AudioData object that stores the encoded audio.</returns>
    private async Task<AudioData<float>> GenerateAudioData(TimedEvents events,
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
            var new_track = AudioData<float>.WithLength(_channels, length);
            mixer.AddTrack(channel, new_track);
        }

        // Map channel tasks.
        await RenderTimedEvents(mixer, events, processedEvents, big_event_length, false, token);

        return mixer.MixDown();
    }

    /// <summary>
    ///     Adds/Subtracts events from an existing AudioMixer.
    /// </summary>
    /// <param name="mixer">The mixer to render into.</param>
    /// <param name="events">The events to render.</param>
    /// <param name="processedEvents">The processed audio samples.</param>
    /// <param name="biggestEventLength">The length of the biggest event in the sequence.</param>
    /// <param name="invert">Whether to subtract the events from the mixer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RenderTimedEvents(AudioMixer mixer, TimedEvents events,
        Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength,
        bool invert = false, CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;

        // Map channel tasks.
        var channels = new Task[_channels];
        for (var i = 0; i < _channels; i++)
        {
            var index = i;

            channels[index] =
                Task.Run(
                    async () =>
                    {
                        await ProcessChannel(mixer, index, events, processedEvents, biggestEventLength, invert);
                    },
                    token);
        }

        // Wait for all tasks to finish.
        await Task.WhenAll(channels);
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
        Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength, bool invert = false)
    {
        var length = mixer.GetLength();
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

        await Parallel.ForAsync(1, working_threads + 1, (i, _) =>
        {
            var start_idx = (i - 1) * chunk_size;
            var end_idx = i * chunk_size;

            var start = (int)start_idx;
            var end = Math.Min((int)end_idx, length);

            if (start > length) return ValueTask.CompletedTask;

            var start_time = Stopwatch.GetTimestamp();
            ProcessChunk(start, end, mixer, channel, events, processedEvents, biggestEventLength, invert);
            var delta = Stopwatch.GetElapsedTime(start_time);
            Log(
                $@"Processed chunk i: {i} in {delta:ss\.ffff} s. Start: {start}, End: {end}, ChunkSize: {chunk_size}, Length: {length}");

            return ValueTask.CompletedTask;
        });
    }

    private void ProcessChunk(int start, int end, AudioMixer mixer, int channel,
        TimedEvents events, Dictionary<(string, double), ProcessedEvent> processedEvents, int biggestEventLength,
        bool invert = false)
    {
        var placement = events.Placement.AsSpan();

        foreach (var current in placement)
        {
            // skip non audible
            if (!current.Audible) continue;

            // get current event start.
            var current_start = (int)current.Index;
            if (current_start < start - biggestEventLength) continue;
            if (current_start >= end) break;

            RenderEventToSlice(start, end, mixer, channel, current, processedEvents, invert);
        }
    }

    private void RenderEventToSlice(int start, int end, AudioMixer mixer, int channel,
        Placement current, Dictionary<(string, double), ProcessedEvent> processedEvents, bool invert = false)
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

        // put pan variable here to be used later
        var pan = 0f;

        switch (current_event)
        {
            // handle #icut event
            case IndividualCutEvent individual_cut_event:
            {
                foreach (var cut_track in from sound in individual_cut_event.CutSounds
                         where mixer.HasTrack(sound)
                         select mixer.GetTrack(sound))
                {
                    var cut_slice = cut_track.GetChannel(channel).AsSpan()[start..end];
                    HandleCut(start, end, current_start, cut_slice);
                }

                return;
            }

            case PannedEvent panned_event:
            {
                pan = Math.Clamp(panned_event.Pan, -1f, 1f);
                break;
            }
        }

        // handle !cut event
        if (event_name == "!cut")
        {
            foreach (var (_, data) in mixer.GetTracks())
                HandleCut(start, end, current_start, data.GetChannel(channel).AsSpan()[start..end]);

            return;
        }

        // search for processed sample
        if (!processedEvents.TryGetValue((event_name, event_value), out var processed_event)) return;

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

        if (delta_end >= mix_slice.Length) delta_end = mix_slice.Length;

        var volume = event_volume;

        // when panning is used dims the channel opposite to what the value represents
        // if it's -1 (left channel only), dims the right channel and vice-versa
        switch (pan)
        {
            // Channel = Right
            case < 0 when channel == 1:
            {
                var percent_dim = 1f + pan;
                volume *= percent_dim;
                break;
            }

            // Channel = Left
            case > 0 when channel == 0:
            {
                var percent_dim = 1f - pan;
                volume *= percent_dim;
                break;
            }
        }

        RenderSample(current_channel, mix_slice, delta_start,
            volume, delta_end, offset, invert);
    }

    private void HandleCut(int start, int end, int currentStart, Span<float> mixSlice)
    {
        var wanted_zero_samples = 4096 * _sampleRate / 48000;
        var norm_start = currentStart - start;
        var norm_end = end - start;

        var zero_samples = 0;
        var zero_index = norm_end;
        for (var i = norm_start; i < norm_end; i++)
        {
            if (zero_samples >= wanted_zero_samples)
            {
                zero_index = i;
                break;
            }

            zero_samples++;

            if (i >= 0 && mixSlice[i] == 0f) continue;
            zero_samples = 0;
        }

        var cut_fade_ms = (int)_settings.CutFadeLengthMs;
        var cut_fade_length = (int)(_settings.SampleRate / 1000) * cut_fade_ms;
        var cut_fade_end = norm_start + cut_fade_length;

        int cut_i;
        for (cut_i = norm_start; cut_i < cut_fade_end; cut_i++)
        {
            if (cut_i < 0 || cut_i >= zero_index) continue;
            var norm_i = cut_fade_end - cut_i;

            var delta = (float)norm_i / cut_fade_length;
            mixSlice[cut_i] *= delta;
        }

        for (var i = cut_i; i < zero_index; i++)
        {
            if (i < 0) continue;
            mixSlice[i] = 0f;
        }
    }

    /// <summary>
    ///     Exports an AudioData object as a WAVE file.
    /// </summary>
    /// <param name="location">The location you want to export to.</param>
    /// <param name="data">The AudioData object.</param>
    public void WriteAsWavFile(string location, AudioData<float> data)
    {
        var stream = File.Open(location, FileMode.Create);
        WriteAsWavFile(stream, data);
    }

    public void WriteAsWavFile(Stream stream, AudioData<float> data)
    {
        if (_settings.EnableNormalization)
            data.Normalize();

        var samples = data.Samples;
        for (var i = 0; i < samples.Length; i++)
        {
            var arr = samples[i];
            samples[i] = arr.TrimEnd();
        }

        var writer = new BinaryWriter(stream);
        var maxLength = samples.Max(r => r.Length);
        AddWavHeader<float>(writer, maxLength);

        var every_n_report = maxLength / 200; // 200 calls.
        for (var i = 0; i < maxLength; i++)
        {
            if (i % every_n_report == 0) IndexReport((ulong)i, (ulong)maxLength);
            for (var j = 0; j < _channels; j++)
                writer.Write(samples[j].Length > i ? samples[j][i] : 0f);
        }

        writer.Flush();
        writer.Close();

        Log("Saved audio file.");
    }

    /// <summary>
    ///     This method adds the RIFF WAVE header to an empty file.
    /// </summary>
    /// <param name="writer">An open BinaryWriter</param>
    /// <param name="dataLength">Length of the audio data.</param>
    private void AddWavHeader<T>(BinaryWriter writer, int dataLength) where T : struct
    {
        ReadOnlySpan<char> riff_header = ['R', 'I', 'F', 'F'];
        ReadOnlySpan<char> wave_header = ['W', 'A', 'V', 'E'];
        ReadOnlySpan<char> fmt_header = ['f', 'm', 't', ' '];
        ReadOnlySpan<char> data_header = ['d', 'a', 't', 'a'];

        var is_float = typeof(T) == typeof(float) || typeof(T) == typeof(double);
        var byte_size = Marshal.SizeOf<T>();
        var length = dataLength * (int)_channels;
        writer.Write(riff_header); // RIFF Chunk Descriptor
        writer.Write(4 + 8 + 16 + 8 + length * 2); // Sub Chunk 1 Size
        //Chunk Size 4 bytes.
        writer.Write(wave_header);
        // fmt sub-chunk
        writer.Write(fmt_header);
        writer.Write(16); // Sub Chunk 1 Size
        writer.Write((short)(is_float ? 3 : 1)); // Audio Format 1 = PCM / 3 = Float
        writer.Write((short)_channels); // Audio Channels
        writer.Write((int)_sampleRate); // Sample Rate
        writer.Write((int)(_sampleRate * _channels * byte_size /* Bytes */)); // Byte Rate
        writer.Write((short)(_channels * byte_size)); // Block Align
        writer.Write((short)(byte_size * 8)); // Bits per Sample
        // data sub-chunk
        writer.Write(data_header);
        writer.Write(length * byte_size); // Sub Chunk 2 Size.
    }

    #region Sample Processing Methods

    /// <summary>
    ///     Adds a source audio data array to a destination.
    /// </summary>
    /// <param name="source">The source audio data you want to add.</param>
    /// <param name="destination">The destination you want to add to.</param>
    /// <param name="index">The index of the destination you want to start on.</param>
    /// <param name="length">The length of the export you want to do.</param>
    /// <param name="volume">The volume of the source audio while being added.</param>
    /// <param name="offset">The source sample offset. Used in multithreading.</param>
    /// <param name="invert">Whether to invert the sample so that if exists in the audio already it gets removed.</param>
    private static void RenderSample(Span<float> source, Span<float> destination, int index,
        double volume, int length = -1, int offset = -1, bool invert = false)
    {
        if (length == -1) length = source.Length;

        if (offset < 0) offset = 0;

        var s_slice = source.Slice(offset, length);
        var d_slice = destination[index..];
        var chunk_size = Vector<float>.Count;
        var final_volume = (float)volume / 100f;
        if (final_volume > 1f) final_volume = MathF.Sqrt(final_volume);

        var s_chunked = s_slice.Length - s_slice.Length % chunk_size;
        var d_chunked = d_slice.Length - d_slice.Length % chunk_size;

        var min = Math.Min(s_chunked, d_chunked);

        for (var i = 0; i < min; i += chunk_size)
        {
            var i_chunk = i + chunk_size;

            var s = s_slice[i..i_chunk];
            var d = d_slice[i..i_chunk];

            var d_vector = new Vector<float>(d);
            var s_vector = new Vector<float>(s);

            var src = s_vector * final_volume;
            var final = invert ? src - d_vector : src + d_vector;

            final.CopyTo(d);
        }

        var min_final = Math.Min(d_slice.Length, s_slice.Length);
        for (var i = min; i < min_final; i++)
        {
            var src = s_slice[i] * final_volume;
            if (invert) d_slice[i] -= src;
            else d_slice[i] += src;
        }
    }

    #endregion
}
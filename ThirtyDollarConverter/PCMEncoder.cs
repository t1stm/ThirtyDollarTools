using System;
using System.Collections.Generic;
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
    private readonly uint Channels;
    private readonly SemaphoreSlim IndexLock = new(1);
    private readonly SampleProcessor SampleProcessor;
    private readonly uint SampleRate;
    private readonly EncoderSettings Settings;

    /// <summary>
    ///     Creates a TDW sequence encoder.
    /// </summary>
    /// <param name="samples">The sample holder that stores the sequence's samples.</param>
    /// <param name="settings">The encoder's settings.</param>
    /// <param name="loggerAction">Action that handles log messages.</param>
    /// <param name="indexReport">Action that recieves encode progress.</param>
    /// <exception cref="Exception">Exception that is thrown when the amount of channels is invalid.</exception>
    public PcmEncoder(SampleHolder samples, EncoderSettings settings,
        Action<string>? loggerAction = null,
        Action<ulong, ulong>? indexReport = null)
    {
        Settings = settings;
        Holder = samples;
        Channels = settings.Channels;
        SampleRate = settings.SampleRate;

        Log = loggerAction ?? (_ => { });
        IndexReport = indexReport ?? ((_, _) => { });
        SampleProcessor = new SampleProcessor(Holder.SampleList, settings, Log);
        PlacementCalculator = new PlacementCalculator(settings, Log);

        switch (Channels)
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
    /// <returns>An AudioData object that stores the encoded audio.</returns>
    public async Task<AudioData<float>> GetMultipleSequencesAudio(IEnumerable<Sequence> sequences)
    {
        var enumerable = sequences as Sequence[] ?? sequences.ToArray();
        var placement = PlacementCalculator.CalculateMany(enumerable);
        var placement_array = placement.ToArray();

        var timed_events = new TimedEvents
        {
            Sequences = enumerable.ToArray(),
            Placement = placement_array,
            TimingSampleRate = (int)SampleRate
        };

        Log("Calculated placement. Starting sample processing.");

        var processed_events = await GetAudioSamples(timed_events);

        Log("Finished processing all samples. Starting audio mixing.");
        var audioData = await GenerateAudioData(timed_events, processed_events);
        return audioData;
    }

    /// <summary>
    ///     This method starts the encoding process.
    /// </summary>
    /// <param name="sequence">The sequence you want to encode.</param>
    /// <returns>An AudioData object that stores the encoded audio.</returns>
    public async Task<AudioData<float>> GetSequenceAudio(Sequence sequence)
    {
        var copy = sequence.Copy(); // To avoid making any changes to the original sequence.
        var placement = PlacementCalculator.CalculateOne(copy);
        var placement_array = placement.ToArray();

        var timed_events = new TimedEvents
        {
            Sequences = [sequence],
            Placement = placement_array,
            TimingSampleRate = (int)SampleRate
        };

        var processed_events = await GetAudioSamples(timed_events);

        Log("Constructing audio.");
        var audioData = await GenerateAudioData(timed_events, processed_events);
        return audioData;
    }

    /// <summary>
    ///     This method gets all resampled audio samples.
    /// </summary>
    /// <param name="events">The calculated events.</param>
    /// <returns>An array containing all processed events.</returns>
    /// <exception cref="Exception">Edge case that only can happen if something is wrong with the program.</exception>
    public async Task<Dictionary<(string, double), ProcessedEvent>> GetAudioSamples(TimedEvents events)
    {
        var placement = events.Placement;
        // Get only unique events. Duplicates get removed.
        var event_dictionary = new Dictionary<(string event_name, double event_value), BaseEvent>();

        foreach (var p in placement)
        {
            if (!p.Audible) continue;

            var ev = p.Event;
            var event_name = ev.SoundEvent ?? string.Empty;
            var event_value = ev.Value;
            if (event_name == "!cut" || ev is ICustomActionEvent) continue;

            event_dictionary.TryAdd((event_name, event_value), ev);
        }

        // Start setting up tasks to process all events.
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
        var task = Parallel.ForEachAsync(processed_events, async (processed_event, token) =>
        {
            processed_event.ProcessAudioData(SampleProcessor);
            await IndexLock.WaitAsync(token);

            finished_tasks++;
            IndexReport(finished_tasks, total_tasks);

            IndexLock.Release();
        });

        var dictionary = new Dictionary<(string, double), ProcessedEvent>();
        var idx = 0;
        foreach (var tuple in event_dictionary.Keys)
        {
            dictionary.Add(tuple, processed_events_memory.Span[idx]);
            idx++;
        }

        await task;
        return dictionary;
    }

    /// <summary>
    ///     This method creates the final audio.
    /// </summary>
    /// <param name="events">The placement of each event.</param>
    /// <param name="processed_events">The resampled sounds.</param>
    /// <param name="cancellation_token">A token that cancels the waiting task.</param>
    /// <returns>An AudioData object that stores the encoded audio.</returns>
    private async Task<AudioData<float>> GenerateAudioData(TimedEvents events,
        Dictionary<(string, double), ProcessedEvent> processed_events,
        CancellationToken? cancellation_token = null)
    {
        var token = cancellation_token ?? CancellationToken.None;
        var last_placement = events.Placement[^1];

        var big_event = processed_events.Values.MaxBy(e => e.AudioData.GetLength());
        if (big_event == null) throw new Exception("No processed events.");

        var big_event_length = big_event.AudioData.GetLength();
        var length = (int)last_placement.Index + big_event_length;
        var audio_data = AudioData<float>.WithLength(Channels, length);

        var mixer = new AudioMixer(audio_data);
        foreach (var sequence in events.Sequences)
        foreach (var channel in sequence.SeparatedChannels)
        {
            var new_track = AudioData<float>.WithLength(Channels, length);
            mixer.AddTrack(channel, new_track);
        }

        // Map channel tasks.
        var channels = new Task[Channels];
        for (var i = 0; i < Channels; i++)
        {
            var index = i;

            channels[index] =
                Task.Run(
                    async () => { await ProcessChannel(mixer, index, events, processed_events, big_event_length); },
                    token);
        }

        // Wait for all tasks to finish.
        await Task.WhenAll(channels);
        return mixer.MixDown();
    }

    public void SetMultithreadingSlices(int thread_count)
    {
        Settings.MultithreadingSlices = thread_count;
    }

    /// <summary>
    ///     Processes an export channel.
    /// </summary>
    /// <param name="mixer">The channel you want to work on.</param>
    /// <param name="channel">The channel ID.</param>
    /// <param name="events">The calculated events.</param>
    /// <param name="processed_events">The processed events for the sequence.</param>
    /// <param name="biggest_event_length">The sequence's biggest event's length.</param>
    private async Task ProcessChannel(AudioMixer mixer, int channel, TimedEvents events,
        Dictionary<(string, double), ProcessedEvent> processed_events, int biggest_event_length)
    {
        var length = mixer.GetLength();
        var min_length_per_thread = Math.Min(1 << 15, length);
        var working_threads = Settings.MultithreadingSlices;

        var min_length_for_working_threads = min_length_per_thread * working_threads;
        while (min_length_for_working_threads > length && working_threads > 1)
            min_length_for_working_threads = min_length_per_thread * --working_threads;

        var ratio = (float)length / min_length_for_working_threads;
        if (ratio < 1)
            throw new Exception($"Invalid calculation of seperated threads.\n" +
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

            var start_time = DateTime.Now;
            ProcessChunk(start, end, mixer, channel, events, processed_events, biggest_event_length);
            var end_time = DateTime.Now;
            Log(
                $@"Processed chunk i: {i} in {end_time - start_time:ss\.ffff} s. Start: {start}, End: {end}, ChunkSize: {chunk_size}, Length: {length}");

            return ValueTask.CompletedTask;
        });
    }

    private void ProcessChunk(int start, int end, AudioMixer mixer, int channel,
        TimedEvents events, Dictionary<(string, double), ProcessedEvent> processed_events, int biggest_event_length)
    {
        var placement = events.Placement.AsSpan();

        foreach (var current in placement)
        {
            // skip non audible
            if (!current.Audible) continue;

            // extract event values
            var current_event = current.Event;
            var (event_name, event_value, event_volume) = current.Event;
            event_name ??= string.Empty;

            // get specified event track if exists
            var track_data = mixer.GetTrackOrDefault(event_name);
            var channel_data = track_data.GetChannel(channel).AsSpan();
            var mixer_length = mixer.GetLength();

            if (mixer_length != channel_data.Length)
                throw new Exception(
                    $"Missmatch between channel and mixer length. Mixer: {mixer_length}, Channel: {channel_data.Length}");

            if (start > channel_data.Length || end > channel_data.Length)
                throw new Exception($"Trying to make a slice bigger than the mixer's length. " +
                                    $"Length: {channel_data.Length}, Start: {start}, End: {end}");

            // slice only memory which will be worked on
            var mix_slice = channel_data[start..end];

            // get current event start.
            var current_start = (int)current.Index;
            if (current_start < start - biggest_event_length) continue;
            if (current_start >= end) break;

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

                    continue;
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
                HandleCut(start, end, current_start, mix_slice);
                continue;
            }

            // search for processed sample
            if (!processed_events.TryGetValue((event_name, event_value), out var processed_event)) continue;

            // get its length
            var current_length = processed_event.AudioData.GetLength();
            // get the channel that the mixer is also on
            var current_channel = processed_event.AudioData.GetChannel(channel);

            // case: event not valid for current mixer slice
            if (current_start + current_length < start) continue;

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
                volume, delta_end, offset);
        }
    }

    private void HandleCut(int start, int end, int current_start, Span<float> mix_slice)
    {
        var WANTED_ZERO_SAMPLES = 4096 * SampleRate / 48000;
        var norm_start = current_start - start;
        var norm_end = end - start;

        var zero_samples = 0;
        var zero_index = norm_end;
        for (var i = norm_start; i < norm_end; i++)
        {
            if (zero_samples >= WANTED_ZERO_SAMPLES)
            {
                zero_index = i;
                break;
            }

            zero_samples++;

            if (i >= 0 && mix_slice[i] == 0f) continue;
            zero_samples = 0;
        }

        var cut_fade_ms = (int)Settings.CutFadeLengthMs;
        var cut_fade_length = (int)(Settings.SampleRate / 1000) * cut_fade_ms;
        var cut_fade_end = norm_start + cut_fade_length;

        int cut_i;
        for (cut_i = norm_start; cut_i < cut_fade_end; cut_i++)
        {
            if (cut_i < 0 || cut_i >= zero_index) continue;
            var norm_i = cut_fade_end - cut_i;

            var delta = (float)norm_i / cut_fade_length;
            mix_slice[cut_i] *= delta;
        }

        for (var i = cut_i; i < zero_index; i++)
        {
            if (i < 0) continue;
            mix_slice[i] = 0f;
        }
    }

    /// <summary>
    ///     Exports an AudioData object as a WAVE file.
    /// </summary>
    /// <param name="location">The location you want to export to.</param>
    /// <param name="data">The AudioData object.</param>
    public void WriteAsWavFile(string location, AudioData<float> data)
    {
        var samples = data.Samples;
        for (var i = 0; i < samples.Length; i++)
        {
            var arr = samples[i];
            if (Settings.EnableNormalization)
                arr.NormalizeVolume();
            samples[i] = arr.TrimEnd();
        }

        var stream = new BinaryWriter(File.Open(location, FileMode.Create));
        var maxLength = samples.Max(r => r.Length);
        AddWavHeader<float>(stream, maxLength);

        var every_n_report = maxLength / 200; // 200 calls.
        for (var i = 0; i < maxLength; i++)
        {
            if (i % every_n_report == 0) IndexReport((ulong)i, (ulong)maxLength);
            for (var j = 0; j < Channels; j++)
                stream.Write(samples[j].Length > i ? samples[j][i] : 0f);
        }

        stream.Flush();
        stream.Close();

        Log("Saved audio file.");
    }

    /// <summary>
    ///     This method adds the RIFF WAVE header to an empty file.
    /// </summary>
    /// <param name="writer">An open BinaryWriter</param>
    /// <param name="data_length">Length of the audio data.</param>
    private void AddWavHeader<T>(BinaryWriter writer, int data_length) where T : struct
    {
        ReadOnlySpan<char> riff_header = stackalloc char[] { 'R', 'I', 'F', 'F' };
        ReadOnlySpan<char> wave_header = stackalloc char[] { 'W', 'A', 'V', 'E' };
        ReadOnlySpan<char> fmt_header = stackalloc char[] { 'f', 'm', 't', ' ' };
        ReadOnlySpan<char> data_header = stackalloc char[] { 'd', 'a', 't', 'a' };

        var is_float = typeof(T) == typeof(float) || typeof(T) == typeof(double);
        var byte_size = Marshal.SizeOf<T>();
        var length = data_length * (int)Channels;
        writer.Write(riff_header); // RIFF Chunk Descriptor
        writer.Write(4 + 8 + 16 + 8 + length * 2); // Sub Chunk 1 Size
        //Chunk Size 4 bytes.
        writer.Write(wave_header);
        // fmt sub-chunk
        writer.Write(fmt_header);
        writer.Write(16); // Sub Chunk 1 Size
        writer.Write((short)(is_float ? 3 : 1)); // Audio Format 1 = PCM / 3 = Float
        writer.Write((short)Channels); // Audio Channels
        writer.Write((int)SampleRate); // Sample Rate
        writer.Write((int)(SampleRate * Channels * byte_size /* Bytes */)); // Byte Rate
        writer.Write((short)(Channels * byte_size)); // Block Align
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
    private static void RenderSample(Span<float> source, Span<float> destination, int index,
        double volume, int length = -1, int offset = -1)
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
            var final = src + d_vector;

            final.CopyTo(d);
        }

        var min_final = Math.Min(d_slice.Length, s_slice.Length);
        for (var i = min; i < min_final; i++)
        {
            var src = s_slice[i] * final_volume;
            d_slice[i] += src;
        }
    }

    #endregion
}
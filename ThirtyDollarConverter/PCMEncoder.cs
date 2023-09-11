using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;

namespace ThirtyDollarConverter;

public class PcmEncoder
{
    private readonly EncoderSettings Settings;
    private readonly uint Channels;
    private readonly uint SampleRate;
    private readonly SampleProcessor SampleProcessor;
    private SampleHolder Holder { get; }
    private Action<string> Log { get; }
    private Action<ulong, ulong> IndexReport { get; }
    private PlacementCalculator PlacementCalculator { get; }
    
    /// <summary>
    /// Creates a TDW composition encoder.
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

        Log = loggerAction ?? new Action<string>(_ => { });
        IndexReport = indexReport ?? new Action<ulong, ulong>((_, _) => { });
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
    
    /// <summary>
    /// This method starts the encoding process.
    /// </summary>
    /// <param name="composition">The composition you want to encode.</param>
    /// <param name="threadCount">How many threads to use for resampling.</param>
    /// <returns>An AudioData object that stores the encoded audio.</returns>
    public AudioData<float> SampleComposition(Composition composition, int threadCount = -1)
    {
        var copy = composition.Copy(); // To avoid making any changes to the original composition.
        var placement = PlacementCalculator.Calculate(copy);

        var (processedEvents, queue) = GetAudioSamples(threadCount, placement.ToArray()).Result;

        for (var i = 0; i < processedEvents.Count; i++)
        {
            var ev = processedEvents[i];
            Log(
                $"({i}): Event: \'{ev.Name}\' {ev.AudioData.Samples[0].Length} - [{ev.Value}] - ({ev.Volume})");
        }

        Log("Constructing audio.");
        var audioData = GenerateAudioData(queue, processedEvents).Result;
        return audioData;
    }

    /// <summary>
    /// This method gets all resampled audio samples.
    /// </summary>
    /// <param name="threadCount">How many threads to use for resampling.</param>
    /// <param name="placement">The calculated placement for each event.</param>
    /// <param name="cancellationToken">Optional cancellation token that allows the resampling process to stop.</param>
    /// <returns>A Tuple containing the processed events and a queue of their placement.</returns>
    /// <exception cref="Exception">Edge case that only can happen if something is wrong with the program.</exception>
    public async Task<Tuple<List<ProcessedEvent>, Queue<Placement>>> GetAudioSamples(int threadCount,
        Placement[] placement,
        CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;

        // Get host thread count or use specified count and make an array of threads.
        var processorCount = threadCount == -1 ? Environment.ProcessorCount : threadCount;
        var threads = new Task[processorCount];
        for (var i = 0; i < threads.Length; i++) threads[i] = Task.CompletedTask;

        var processedEvents = new List<ProcessedEvent>();

        // I want to avoid multiple enumeration of the Placement IEnumerable. (totally not because Rider complains about it. no....)
        var queue = new Queue<Placement>();

        var currentThread = 0;
        for (ulong i = 0; i < (ulong) placement.LongLength; i++)
        {
            var current = placement[i];
            // Wait for the previous thread to finish its work.
            await threads[currentThread].WaitAsync(token);

            var ev = current.Event;
            queue.Enqueue(current);
            if ((ev.SoundEvent?.StartsWith('!') ?? false) || ev.SoundEvent is "#!cut") continue;
            lock (processedEvents)
            {
                if (processedEvents.Any(r => r.Name == ev.SoundEvent && Math.Abs(r.Value - ev.Value) < 0.01))
                    continue;
            }

            // Adding the event here to prevent any thread fighting.
            var processed = new ProcessedEvent
            {
                Name = ev.SoundEvent ??
                       throw new Exception(
                           $"Event name is null at index: \'{current.Index}\' after placement pass."),
                Value = ev.Value,
                Volume = ev.Volume ?? 100,
                AudioData = AudioData<float>.Empty(Channels)
            };

            var thread = threads[currentThread] = new Task(() =>
            {
                processed.AudioData = SampleProcessor.ProcessEvent(ev);
                lock (processedEvents)
                {
                    processedEvents.Add(processed);
                }
            });
            thread.Start();

            if (++currentThread >= processorCount) currentThread = 0;
        }

        foreach (var thread in threads) await thread.WaitAsync(token);

        return new Tuple<List<ProcessedEvent>, Queue<Placement>>(processedEvents, queue);
    }

    /// <summary>
    /// This method creates the final audio.
    /// </summary>
    /// <param name="queue">The placement of each event.</param>
    /// <param name="processedEvents">The resampled sounds.</param>
    /// <param name="cancellationToken">A token that cancels the waiting task.</param>
    /// <returns>An AudioData object that stores the encoded audio.</returns>
    private async Task<AudioData<float>> GenerateAudioData(Queue<Placement> queue,
        IReadOnlyCollection<ProcessedEvent> processedEvents,
        CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;
        var audioData = AudioData<float>.Empty(Channels);

        var encodeTasks = new Task[Channels];
        var encodeIndices = new ulong[Channels];

        for (var channelIndex = 0; channelIndex < Channels; channelIndex++)
        {
            var indexCopy = channelIndex;
            encodeTasks[indexCopy] = new Task(() =>
            {
                var old_placement = 0ul;
                var difference = 0u;
                
                ulong current_index = 0;
                var length = (ulong) queue.LongCount();
                var update_n_length = (ulong) Math.Ceiling((double)length / 100);
                
                foreach (var placement in queue.Where(placement => placement.Audible))
                {
                    if (placement.Index != old_placement)
                    {
                        old_placement = placement.Index;
                        difference = 0;
                    }
                    else
                    {
                        difference += Settings.CombineDelayMs * SampleRate / 1000;
                    }
                    
                    if (current_index != 0 && current_index % update_n_length == 0)
                    {
                        IndexReport(current_index, length);
                    }
                    
                    var ev = placement.Event;
                    if (ev.SoundEvent == "#!cut")
                    {
                        var end = (ulong)audioData.Samples[indexCopy].LongLength;
                        var real_cut = placement.Index + SampleRate * (Settings.CutDelayMs / 1000);
                        var delta = real_cut - placement.Index;
                        
                        lock (audioData.Samples[indexCopy])
                        {
                            var delta_time = 0;
                            for (var k = placement.Index; k < real_cut; k++)
                            {
                                audioData.Samples[indexCopy][k] *= 1 - (float) delta_time / delta;
                                delta_time++;
                            }

                            for (var cut = real_cut; cut < end; cut++)
                            {
                                audioData.Samples[indexCopy][cut] = 0f;
                            }
                        }

                        continue;
                    }

                    var sample =
                        processedEvents.First(r => r.Name == ev.SoundEvent && Math.Abs(r.Value - ev.Value) < 1)
                            .AudioData;

                    var data = sample.GetChannel(indexCopy);
                    RenderSample(data, ref audioData.Samples[indexCopy], placement.Index + difference, ev.Volume ?? 100);
                    encodeIndices[indexCopy] = placement.Index;
                    current_index++;
                }
            }, token);
            encodeTasks[indexCopy].Start();
        }

        await Task.WhenAll(encodeTasks);

        return audioData;
    }

    private static ulong Sum(ulong[] source)
    {
        ulong result = 0;
        for (ulong i = 0; i < (ulong) source.LongLength; i++) 
            result += source[i] / 1000;
        return result * 1000;
    }

    /// <summary>
    /// Exports an AudioData object as a WAVE file.
    /// </summary>
    /// <param name="location">The location you want to export to.</param>
    /// <param name="data">The AudioData object.</param>
    public void WriteAsWavFile(string location, AudioData<float> data)
    {
        var samples = data.Samples;
        for (var i = 0; i < samples.Length; i++)
        {
            var arr = samples[i];
            arr.NormalizeVolume();
            samples[i] = arr.TrimEnd();
        }

        var stream = new BinaryWriter(File.Open(location, FileMode.Create));
        var maxLength = samples.Max(r => r.Length);
        AddWavHeader(stream, maxLength);
        stream.Write((short)0);

        var every_n_report = maxLength / 200; // 200 calls.
        for (var i = 0; i < maxLength; i++)
        {
            if (i % every_n_report == 0)
            {
                IndexReport((ulong)i, (ulong)maxLength);
            }
            for (var j = 0; j < Channels; j++)
                if (samples[j].Length > i)
                    stream.Write((short)(samples[j][i] * 32768));
                else stream.Write((short)0);
        }

        stream.Flush();
        stream.Close();

        Log("Saved audio file.");
    }

    /// <summary>
    /// This method adds the RIFF WAVE header to an empty file.
    /// </summary>
    /// <param name="writer">An open BinaryWriter</param>
    /// <param name="dataLength">Length of the audio data.</param>
    private void AddWavHeader(BinaryWriter writer, int dataLength)
    {
        var length = dataLength * (int)Channels;
        writer.Write(new[] { 'R', 'I', 'F', 'F' }); // RIFF Chunk Descriptor
        writer.Write(4 + 8 + 16 + 8 + length * 2); // Sub Chunk 1 Size
        //Chunk Size 4 bytes.
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        // fmt sub-chunk
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16); // Sub Chunk 1 Size
        writer.Write((short)1); // Audio Format 1 = PCM
        writer.Write((short)Channels); // Audio Channels
        writer.Write((int)SampleRate); // Sample Rate
        writer.Write((int)SampleRate * (int)Channels * 2 /* Bytes */); // Byte Rate
        writer.Write((short)(Channels * 2)); // Block Align
        writer.Write((short)16); // Bits per Sample
        // data sub-chunk
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(length * 2); // Sub Chunk 2 Size.
    }

    public struct ProcessedEvent
    {
        public string Name;
        public double Value;
        public double Volume;
        public AudioData<float> AudioData;
    }

    #region Sample Processing Methods
    
    /// <summary>
    /// Adds a source audio data array to a destination.
    /// </summary>
    /// <param name="source">The source audio data you want to add.</param>
    /// <param name="destination">The destination you want to add to.</param>
    /// <param name="index">The index of the destination you want to start on.</param>
    /// <param name="volume">The volume of the source audio while being added.</param>
    private static void RenderSample(float[] source, ref float[] destination, ulong index, double volume)
    {
        lock (destination)
        {
            for (ulong i = 0; i < (ulong)source.LongLength; i++)
            {
                var data = source[i];
                ModifyAt(ref destination, (float)(data * (volume / 100)), index + i);
            }
        }
    }

    /// <summary>
    /// Modifies the destination audio with a sample.
    /// </summary>
    /// <param name="destination">The destination audio array.</param>
    /// <param name="data">The index of the destination you want to add to.</param>
    /// <param name="index">The index to the destination</param>
    private static void ModifyAt(ref float[] destination, float data, ulong index)
    {
        lock (destination)
        {
            if (index < (ulong)destination.LongLength)
            {
                destination[index] = MixSamples(data, destination[index]);
                return;
            }

            if (index >= (ulong)destination.LongLength) FillWithZeros(ref destination, index);
            destination[index] = data;
        }
    }

    /// <summary>
    /// Wrapper method for mixing samples. (to easily implement another mixing standard if needed)
    /// </summary>
    /// <param name="sampleOne">The first sample</param>
    /// <param name="sampleTwo">The second sample.</param>
    /// <returns>The mixed sample.</returns>
    private static float MixSamples(float sampleOne, float sampleTwo)
    {
        return sampleOne + sampleTwo;
    }
    
    /// <summary>
    /// Fills an array with zeros starting from the index.
    /// </summary>
    /// <param name="data">The destination.</param>
    /// <param name="index">The index to start from.</param>
    private static void FillWithZeros(ref float[] data, ulong index)
    {
        var old = data;
        data = new float[(ulong)(index * 1.5)];
        for (ulong i = 0; i < (ulong)old.LongLength; i++) data[i] = old[i];
    }

    #endregion
}
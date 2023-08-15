#nullable enable
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
    private readonly uint Channels;
    private readonly uint SampleRate;
    private readonly SampleProcessor SampleProcessor;
    private SampleHolder Holder { get; }
    public Composition Composition { get; }
    private Action<string> Log { get; }
    private Action<ulong, ulong> IndexReport { get; }
    private PlacementCalculator PlacementCalculator { get; }
    
    public PcmEncoder(SampleHolder samples, Composition composition, EncoderSettings settings,
        Action<string>? loggerAction = null,
        Action<ulong, ulong>? indexReport = null)
    {
        Holder = samples;
        Composition = composition;
        Channels = settings.Channels;
        SampleRate = settings.SampleRate;

        Log = loggerAction ?? new Action<string>(_ => { });
        IndexReport = indexReport ?? new Action<ulong, ulong>((_, _) => { });
        SampleProcessor = new SampleProcessor(Holder.SampleList, settings, Log);
        PlacementCalculator = new PlacementCalculator(settings);
        
        switch (Channels)
        {
            case < 1:
                throw new Exception("Having less than one channel is literally impossible.");
            case > 2:
                throw new Exception("Having more than two audio channels isn't currently supported.");
        }
    }

    public AudioData<float> SampleComposition(Composition composition, int threadCount = -1)
    {
        var copy = composition.Copy(); // To avoid making any changes to the original composition.
        var placement = PlacementCalculator.Calculate(copy);

        var (processedEvents, queue) = GetAudioSamples(threadCount, placement).Result;

        for (var i = 0; i < processedEvents.Count; i++)
        {
            var ev = processedEvents[i];
            Console.WriteLine(
                $"({i}): Event: \'{ev.Name}\' {ev.AudioData.Samples[0].Length} - [{ev.Value}] - ({ev.Volume})");
        }

        Console.WriteLine("Constructing audio.");
        var audioData = GenerateAudioData(queue, processedEvents).Result;
        return audioData;
    }

    private async Task<Tuple<List<ProcessedEvent>, Queue<Placement>>> GetAudioSamples(int threadCount,
        IEnumerable<Placement> placement,
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
        foreach (var current in placement)
        {
            // Wait for the previous thread to finish its work.
            await threads[currentThread].WaitAsync(token);

            var ev = current.Event;
            queue.Enqueue(current);
            if (ev.SoundEvent == "#!cut") continue;
            lock (processedEvents)
            {
                if (processedEvents.Any(r => r.Name == ev.SoundEvent && Math.Abs(r.Value - ev.Value) < 0.5))
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

    private async Task<AudioData<float>> GenerateAudioData(Queue<Placement> queue,
        IReadOnlyCollection<ProcessedEvent> processedEvents,
        CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;
        var audioData = AudioData<float>.Empty(Channels);

        var encodeTasks = new Task[Channels];
        var encodeIndices = new ulong[Channels];
        
        var count = queue.LongCount();

        for (var channelIndex = 0; channelIndex < Channels; channelIndex++)
        {
            var indexCopy = channelIndex;
            encodeTasks[indexCopy] = new Task(() =>
            {
                ulong current_index = 0;
                var length = (ulong) queue.LongCount();
                
                foreach (var placement in queue)
                {
                    //Log($"({indexCopy}) Processing: {placement.Index}");
                    //IndexReport(current_index, length);
                    var ev = placement.Event;
                    if (ev.SoundEvent == "#!cut")
                    {
                        var end = (ulong)audioData.Samples[indexCopy].LongLength;
                        lock (audioData.Samples[indexCopy])
                        {
                            for (var k = placement.Index; k < end; k++)
                                audioData.Samples[indexCopy][k] = 0f;
                        }

                        continue;
                    }

                    var sample =
                        processedEvents.First(r => r.Name == ev.SoundEvent && Math.Abs(r.Value - ev.Value) < 1)
                            .AudioData;

                    var data = sample.GetChannel(indexCopy);
                    RenderSample(data, ref audioData.Samples[indexCopy], placement.Index, ev.Volume ?? 100);
                    encodeIndices[indexCopy] = placement.Index;
                    current_index++;
                }
            });
            encodeTasks[indexCopy].Start();
        }

        var finished = false;

        async void WaitFinish()
        {
            foreach (var task in encodeTasks) await task.WaitAsync(token);
            finished = true;
        }

        var waiter = new Task(WaitFinish);
        waiter.Start();

        while (!finished && !token.IsCancellationRequested)
        {
            IndexReport(Sum(encodeIndices) / (ulong) encodeIndices.Length, (ulong) count);
            await Task.Delay(66, token);
        }

        return audioData;
    }

    private static ulong Sum(ulong[] source)
    {
        ulong result = 0;
        for (ulong i = 0; i < (ulong) source.LongLength; i++) 
            result += source[i] / 1000;
        return result * 1000;
    }

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

        for (var i = 0; i < maxLength; i++)
        for (var j = 0; j < Channels; j++)
            if (samples[j].Length > i)
                stream.Write((short)(samples[j][i] * 32768));
            else stream.Write((short)0);

        stream.Close();
    }

    private void AddWavHeader(BinaryWriter writer, int dataLength)
    {
        var length = dataLength * Channels;
        writer.Write(new[] { 'R', 'I', 'F', 'F' }); // RIFF Chunk Descriptor
        writer.Write(4 + 8 + 16 + 8 + length * 2); // Sub Chunk 1 Size
        //Chunk Size 4 bytes.
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        // fmt sub-chunk
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16); // Sub Chunk 1 Size
        writer.Write((short)1); // Audio Format 1 = PCM
        writer.Write((short)Channels); // Audio Channels
        writer.Write(SampleRate); // Sample Rate
        writer.Write(SampleRate * Channels * 2 /* Bytes */); // Byte Rate
        writer.Write((short)(Channels * 2)); // Block Align
        writer.Write((short)16); // Bits per Sample
        // data sub-chunk
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(length * 2); // Sub Chunk 2 Size.
    }

    private struct ProcessedEvent
    {
        public string Name;
        public double Value;
        public double Volume;
        public AudioData<float> AudioData;
    }

    #region Sample Processing Methods

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

    private static float MixSamples(float sampleOne, float sampleTwo)
    {
        return sampleOne + sampleTwo;
    }


    private static void FillWithZeros(ref float[] data, ulong index)
    {
        var old = data;
        data = new float[(ulong)(index * 1.5)];
        for (ulong i = 0; i < (ulong)old.LongLength; i++) data[i] = old[i];
    }

    #endregion
}
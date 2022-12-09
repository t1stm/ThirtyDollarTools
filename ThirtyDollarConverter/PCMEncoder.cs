using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;

namespace ThirtyDollarConverter
{
    public class PcmEncoder
    {
        public PcmEncoder(SampleHolder samples, Composition composition, EncoderSettings settings, Action<string>? loggerAction = null,
            Action<int, int>? indexReport = null)
        {
            Holder = samples;
            Composition = composition;
            Log = loggerAction ?? new Action<string>(_ => { });
            IndexReport = indexReport ?? new Action<int, int>((_, _) => { });
            SampleRate = settings.SampleRate;
            Channels = settings.Channels;
            Threads = new Thread[Channels];
        }
        
        private readonly int SampleRate;
        private readonly int Channels;
        private List<float[]> PcmBytes { get; set; } = new();
        private Composition Composition { get; }
        private SampleHolder Holder { get; }
        private Dictionary<Sound, PcmDataHolder> Samples => Holder.SampleList;
        private Action<string> Log { get; }
        private Action<int, int> IndexReport { get; }
        private readonly Thread[] Threads;

        private void AddOrChangeByte(int channelIndex, float pcmByte, ulong index)
        {
            lock (PcmBytes[channelIndex])
            {
                if (index < (ulong) PcmBytes[channelIndex].LongLength)
                {
                    PcmBytes[channelIndex][index] = MixSamples(pcmByte, PcmBytes[channelIndex][index]);
                    return;
                }

                if (index >= (ulong) PcmBytes[channelIndex].LongLength) FillWithZeros(channelIndex, index);
                PcmBytes[channelIndex][index] = pcmByte;
            }
        }

        private static float MixSamples(float sampleOne, float sampleTwo)
        {
            return sampleOne + sampleTwo;
        }

        private void FillWithZeros(int channelIndex, ulong index)
        {
            lock (PcmBytes[channelIndex])
            {
                var old = PcmBytes[channelIndex];
                PcmBytes[channelIndex] = new float[(ulong) (index * 1.5)];
                for (ulong i = 0; i < (ulong) old.LongLength; i++)
                {
                    PcmBytes[channelIndex][i] = old[i];
                }
            }
        }

        private static void CalculateVolume(Composition composition)
        {
            if (composition == null) throw new Exception("Null Composition");
            double volume = 100;
            lock (composition.Events)
            {
                foreach (var ev in composition.Events) // Quick pass for volume
                {
                    switch (ev.SoundEvent)
                    {
                        case "!volume":
                            switch (ev.ValueScale)
                            {
                                case ValueScale.Times:
                                    volume *= ev.Value;
                                    break;
                                case ValueScale.Add:
                                    volume += ev.Value;
                                    break;
                                case ValueScale.None:
                                    volume = ev.Value;
                                    break;
                            }

                            break;
                    }

                    ev.Volume = volume;
                }

                composition.Events.RemoveAll(e => e.SoundEvent == "!volume");
            }
        }

        public void Start()
        {
            for (var i = 0; i < Channels; i++)
            {
                var threadIndex = i;
                Threads[i] = new Thread(() => ProcessOnThread(threadIndex));
                PcmBytes.Add(new float[1024]);
            }

            foreach (var thread in Threads)
            {
                thread.Start();
            }

            foreach (var thread in Threads)
            {
                thread.Join();
            }
        }

        public void ProcessOnThread(int channelIndex)
        {
            if (Composition == null) throw new Exception("Null Composition");
            var composition = Composition.Copy();
            var bpm = 300.0;
            var position = (ulong) (SampleRate / (bpm / 60));
            var transpose = 0.0;
            CalculateVolume(composition);

            for (var i = 0; i < composition!.Events.Count; i++)
            {
                var index = position;
                var ev = composition.Events[i];
                IndexReport(i, composition!.Events.Count);
                switch (ev.SoundEvent)
                {
                    case "!speed":
                        switch (ev.ValueScale)
                        {
                            case ValueScale.Times:
                                bpm *= ev.Value;
                                break;
                            case ValueScale.Add:
                                bpm += ev.Value;
                                break;
                            case ValueScale.None:
                                bpm = ev.Value;
                                break;
                        }

                        Log($"({channelIndex}): BPM is now: {bpm}");
                        continue;

                    case "!loopmany" or "!loop":
                        if (ev.PlayTimes <= 0) continue;
                        ev.PlayTimes--;
                        for (var j = i; j > 0; j--)
                        {
                            if (composition.Events[j].SoundEvent != "!looptarget")
                            {
                                continue;
                            }

                            i = j - 1;
                            break;
                        }

                        Log($"({channelIndex}): Going to element: ({i + 1}) - \"{composition.Events[i + 1]}\"");
                        continue;

                    case "!jump":
                        if (ev.PlayTimes <= 0) continue;
                        ev.PlayTimes--;
                        //i = Triggers[(int) ev.Value - 1] - 1;
                        var item = composition.Events.FirstOrDefault(r =>
                            r.SoundEvent == "!target" && (int) r.Value == (int) ev.Value);
                        if (item == null)
                        {
                            Log($"Unable to target with id: {ev.Value}");
                            continue;
                        }

                        i = composition.Events.IndexOf(item) - 1;
                        Log($"({channelIndex}): Jumping to element: ({i}) - {composition.Events[i]}");
                        //
                        continue;

                    case "_pause" or "!stop":
                        Log($"({channelIndex}): Pausing for: {ev.PlayTimes} beats.");
                        while (ev.PlayTimes >= 1)
                        {
                            ev.PlayTimes--;
                            position += (ulong) (SampleRate / (bpm / 60));
                        }

                        ev.PlayTimes = ev.OriginalLoop;
                        continue;

                    case "!cut":
                        for (var j = position + (ulong) (SampleRate / (bpm / 60));
                            j < (ulong) PcmBytes[channelIndex].LongLength;
                            j++)
                        {
                            //TODO: Implement a better muting method.
                            PcmBytes[channelIndex][j] = 0;
                        }

                        continue;

                    case "" or "!looptarget" or "!target" or "!volume" or "!flash" or "!bg":
                        continue;

                    case "!combine":
                        position -= (ulong) (SampleRate / (bpm / 60));
                        continue;

                    case "!transpose":
                        switch (ev.ValueScale)
                        {
                            case ValueScale.Times:
                                transpose *= ev.Value;
                                continue;
                            case ValueScale.Add:
                                transpose += ev.Value;
                                continue;
                            case ValueScale.None:
                                transpose = ev.Value;
                                continue;
                        }

                        continue;

                    default:
                        position += (ulong) (SampleRate / (bpm / 60));
                        break;
                }
                
                Log($"({channelIndex}): Processing Event: [{index}] - \"{ev}\"");
                HandleProcessing(channelIndex, ev, index, -1, transpose);
                switch (ev.SoundEvent)
                {
                    case not ("!transpose" or "!loopmany" or "!volume" or "!flash" or "!combine" or "!speed" or
                        "!looptarget" or "!loop" or "!cut" or "!target" or "!jump" or "_pause" or "!stop"):
                        if (ev.PlayTimes > 1)
                        {
                            ev.PlayTimes--;
                            i--;
                            continue;
                        }

                        ev.PlayTimes = ev.OriginalLoop;
                        continue;
                }
            }
        }

        private class ProcessedSample
        {
            public float[]? SampleData { get; init; }
            public ulong ProcessedChunks { get; set; }
            public ulong SampleLength => (ulong) (SampleData?.LongLength ?? 0);
            public double Volume { get; init; }
        }

        private void HandleProcessing(int channelIndex, Event ev, ulong index, long breakAtIndex, double transpose)
        {
            try
            {
                var (_, value) = Samples.AsParallel().FirstOrDefault(pair => pair.Key.Filename == ev.SoundEvent);
                var sampleData = value.ReadAsFloat32Array(Channels > 1);
                if (sampleData == null)
                    throw new NullReferenceException(
                        $"Sample data is null for event: \"{ev}\", Samples Count is: {Samples.Count}");
                ProcessedSample sample = new()
                {
                    SampleData = Resample(sampleData.GetChannel(channelIndex), value.SampleRate,
                        (uint) (SampleRate / Math.Pow(2, (ev.Value + transpose) / 12)), 1),
                    Volume = ev.Volume
                };

                var size = breakAtIndex == -1 ? sample.SampleLength : (ulong) breakAtIndex;
                for (sample.ProcessedChunks = 0; sample.ProcessedChunks < size; sample.ProcessedChunks++)
                {
                    if (breakAtIndex != -1 && sample.ProcessedChunks >= (ulong) breakAtIndex) return;
                    AddOrChangeByte(
                        channelIndex,
                        (float) ((sample.SampleData?[sample.ProcessedChunks] ?? 0) * (sample.Volume * 0.5 / 100)),
                        index + sample.ProcessedChunks);
                }
            }
            catch (Exception e)
            {
                Log($"Processing failed: \"{e}\"");
            }
        }


        private unsafe float[] Resample(float[] samples, uint sampleRate, uint targetSampleRate, uint channels)
        {
            if (sampleRate == targetSampleRate) return samples;
            fixed (float* vals = samples)
            {
                var length = Resample32BitFloat(vals, null, sampleRate, targetSampleRate, (ulong) samples.LongLength,
                    channels);
                float[] alloc = new float[length];
                fixed (float* output = alloc)
                {
                    Resample32BitFloat(vals, output, sampleRate, targetSampleRate, (ulong) samples.LongLength,
                        channels);
                }

                return alloc;
            }
        }

        // Original Source: https://github.com/cpuimage/resampler

        private unsafe ulong Resample32BitFloat(float* input, float* output, uint inSampleRate, uint outSampleRate,
            ulong inputSize, uint channels)
        {
            if (input == null) return 0;
            var outputSize = (ulong) (inputSize * (double) outSampleRate / inSampleRate);
            outputSize -= outputSize % channels;
            if (output == null) return outputSize;
            var stepDist = inSampleRate / (double) outSampleRate;
            const ulong fixedFraction = (ulong) 1 << 32;
            const double normFixed = 1.0 / ((ulong) 1 << 32);
            var step = (ulong) (stepDist * fixedFraction + 0.5);
            ulong curOffset = 0;
            for (uint i = 0; i < outputSize; i += 1)
            {
                for (uint c = 0; c < channels; c += 1)
                {
                    *output++ = (float) (input[c] + (input[c + channels] - input[c]) *
                        ((curOffset >> 32) + (curOffset & (fixedFraction - 1)) * normFixed));
                }

                curOffset += step;
                input += (curOffset >> 32) * channels;
                curOffset &= fixedFraction - 1;
            }

            return outputSize;
        }

        private unsafe ulong Resample16Bit(short* input, short* output, uint inSampleRate, uint outSampleRate,
            ulong inputSize, uint channels)
        {
            var outputSize = (ulong) (inputSize * (double) outSampleRate / inSampleRate);
            outputSize -= outputSize % channels;
            if (output == null) return outputSize;
            var stepDist = (double) inSampleRate / outSampleRate;
            const ulong fixedFraction = (ulong) 1 << 32;
            const double normFixed = 1.0 / ((ulong) 1 << 32);
            var step = (ulong) (stepDist * fixedFraction + 0.5);
            ulong curOffset = 0;
            for (uint i = 0; i < outputSize; i += 1)
            {
                for (uint c = 0; c < channels; c += 1)
                    *output++ = (short) (input[c] + (input[c + channels] - input[c]) *
                        ((curOffset >> 32) + (curOffset & (fixedFraction - 1)) * normFixed));
                curOffset += step;
                input += (curOffset >> 32) * channels;
                curOffset &= fixedFraction - 1;
            }

            return outputSize;
        }

        public void WriteAsWavFile(string location)
        {
            for (var i = 0; i < PcmBytes.Count; i++)
            {
                var arr = PcmBytes[i];
                arr.NormalizeVolume();
                PcmBytes[i] = arr.TrimEnd();
            }

            var stream = new BinaryWriter(File.Open(location, FileMode.Create));
            AddWavHeader(stream);
            stream.Write((short) 0);

            var maxLength = PcmBytes.Max(r => r.Length);

            for (var i = 0; i < maxLength; i++)
            {
                for (var j = 0; j < Channels; j++)
                {
                    if (PcmBytes[j].Length > i)
                        stream.Write((short) (PcmBytes[j][i] * 32768));
                    else stream.Write((short) 0);
                }
            }

            stream.Close();
        }

        /*public MemoryStream WriteAsWavStream()
        {
            var ms = new MemoryStream();
            PcmBytes.NormalizeVolume();
            PcmBytes = PcmBytes.TrimEnd();
            var stream = new BinaryWriter(ms);
            AddWavHeader(stream);
            stream.Write((short) 0);
            foreach (var data in PcmBytes)
            {
                for (var i = 0; i < Channels; i++)
                {
                    stream.Write((short) (data * 32768));
                }
            }
            stream.Close();
            return ms;
        }*/

        private void AddWavHeader(BinaryWriter writer)
        {
            var length = PcmBytes.Max(r => r.Length) * Channels;
            writer.Write(new[] {'R', 'I', 'F', 'F'}); // RIFF Chunk Descriptor
            writer.Write(4 + 8 + 16 + 8 + length * 2); // Sub Chunk 1 Size
            //Chunk Size 4 bytes.
            writer.Write(new[] {'W', 'A', 'V', 'E'});
            // fmt sub-chunk
            writer.Write(new[] {'f', 'm', 't', ' '});
            writer.Write(16); // Sub Chunk 1 Size
            writer.Write((short) 1); // Audio Format 1 = PCM
            writer.Write((short) Channels); // Audio Channels
            writer.Write(SampleRate); // Sample Rate
            writer.Write(SampleRate * Channels * 2 /* Bytes */); // Byte Rate
            writer.Write((short) (Channels * 2)); // Block Align
            writer.Write((short) 16); // Bits per Sample
            // data sub-chunk
            writer.Write(new[] {'d', 'a', 't', 'a'});
            writer.Write(length * 2); // Sub Chunk 2 Size.
        }
    }
}
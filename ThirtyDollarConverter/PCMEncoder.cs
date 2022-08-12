using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThirtyDollarParser;

namespace ThirtyDollarConverter
{
    public class PcmEncoder
    {
        private const uint SampleRate = 48000; //Hz
        private const int Channels = 1;

        public PcmEncoder(SampleHolder samples, Composition composition, Action<string>? loggerAction = null)
        {
            Holder = samples;
            Composition = composition;
            Log = loggerAction ?? new Action<string>(_ => {  });
        }

        private float[] PcmBytes { get; set; } = new float[1024];
        private Composition Composition { get; }
        private SampleHolder Holder { get; }
        private Dictionary<Sound, short[]?> Samples => Holder.SampleList;
        private Action<string> Log { get; }

        private void AddOrChangeByte(float pcmByte, ulong index)
        {
            lock (PcmBytes)
            {
                if (index < (ulong) PcmBytes.LongLength)
                {
                    PcmBytes[index] = MixSamples(pcmByte, PcmBytes[index]);
                    return;
                }

                if (index >= (ulong) PcmBytes.LongLength) FillWithZeros(index);
                PcmBytes[index] = pcmByte;
            }
        }

        private static float MixSamples(float sampleOne, float sampleTwo)
        {
            return sampleOne + sampleTwo;
        }

        private void FillWithZeros(ulong index)
        {
            lock (PcmBytes)
            {
                var old = PcmBytes;
                PcmBytes = new float[(ulong) (index * 1.5)];
                for (ulong i = 0; i < (ulong) old.LongLength; i++)
                {
                    PcmBytes[i] = old[i];
                }
            }
        }

        private void CalculateVolume()
        {
            if (Composition == null) throw new Exception("Null Composition");
            double volume = 100;
            lock (Composition.Events)
            {
                foreach (var ev in Composition.Events) // Quick pass for volume
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

                Composition.Events.RemoveAll(e => e.SoundEvent == "!volume");
            }
        }

        public void Start()
        {
            if (Composition == null) throw new Exception("Null Composition");
            var bpm = 300.0;
            var position = (ulong) (SampleRate / (bpm / 60));
            var transpose = 0.0;
            CalculateVolume();

            for (var i = 0; i < Composition!.Events.Count; i++)
            {
                var ev = Composition.Events[i];
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

                        Log($"BPM is now: {bpm}");
                        continue;

                    case "!loopmany" or "!loop":
                        if (ev.Loop <= 0) continue;
                        ev.Loop--;
                        for (var j = i; j > 0; j--)
                        {
                            if (Composition.Events[j].SoundEvent != "!looptarget")
                            {
                                continue;
                            }

                            i = j - 1;
                            break;
                        }

                        Log($"Going to element: ({i + 1}) - \"{Composition.Events[i + 1]}\"");
                        continue;

                    case "!jump":
                        if (ev.Loop <= 0) continue;
                        ev.Loop--;
                        //i = Triggers[(int) ev.Value - 1] - 1;
                        var item = Composition.Events.FirstOrDefault(r =>
                            r.SoundEvent == "!target" && (int) r.Value == (int) ev.Value);
                        if (item == null)
                        {
                            Log($"Unable to target with id: {ev.Value}");
                            continue;
                        }

                        i = Composition.Events.IndexOf(item) - 1;
                        Log($"Jumping to element: ({i}) - {Composition.Events[i]}");
                        //
                        continue;

                    case "_pause" or "!stop":
                        Log($"Pausing for: {ev.Loop} beats.");
                        while (ev.Loop >= 1)
                        {
                            ev.Loop--;
                            position += (ulong) (SampleRate / (bpm / 60));
                        }

                        ev.Loop = ev.OriginalLoop;
                        continue;

                    case "!cut":
                        for (var j = position + (ulong) (SampleRate / (bpm / 60));
                            j < (ulong) PcmBytes.LongLength;
                            j++)
                        {
                            PcmBytes[j] = 0;
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

                var index = position;
                Log($"Processing Event: [{index}] - \"{ev}\"");
                HandleProcessing(ev, index, -1, transpose);
                switch (ev.SoundEvent)
                {
                    case not ("!transpose" or "!loopmany" or "!volume" or "!flash" or "!combine" or "!speed" or
                        "!looptarget" or "!loop" or "!cut" or "!target" or "!jump" or "_pause" or "!stop"):
                        if (ev.Loop > 1)
                        {
                            ev.Loop--;
                            i--;
                            continue;
                        }

                        ev.Loop = ev.OriginalLoop;
                        continue;
                }
            }
        }

        private class ProcessedSample
        {
            public short[]? SampleData { get; init; }
            public ulong ProcessedChunks { get; set; }
            public ulong SampleLength => (ulong) (SampleData?.LongLength ?? 0);
            public double Volume { get; init; }
        }

        private void HandleProcessing(Event ev, ulong index, long breakAtIndex, double transpose)
        {
            try
            {
                var (_, value) = Samples.AsParallel().FirstOrDefault(pair => pair.Key.Filename == ev.SoundEvent);
                if (value == null) throw new NullReferenceException($"Sample data is null for event: \"{ev}\", Samples Count is: {Samples.Count}");
                ProcessedSample sample = ev.Value == 0 && transpose == 0.0
                    ? new ProcessedSample {SampleData = value, Volume = ev.Volume}
                    : new ProcessedSample
                    {
                        SampleData = Resample(value, SampleRate,
                            (uint) (SampleRate / Math.Pow(2, (ev.Value + transpose) / 12)), 1),
                        Volume = ev.Volume
                    };

                var size = breakAtIndex == -1 ? sample.SampleLength : (ulong) breakAtIndex;
                for (sample.ProcessedChunks = 0; sample.ProcessedChunks < size; sample.ProcessedChunks++)
                {
                    if (breakAtIndex != -1 && sample.ProcessedChunks >= (ulong) breakAtIndex) return;
                    AddOrChangeByte(
                        (float) ((sample.SampleData?[sample.ProcessedChunks] ?? 0) * (sample.Volume * 0.5 / 100)) /
                        32768f, index + sample.ProcessedChunks);
                }
            }
            catch (Exception e)
            {
                Log($"Processing failed: \"{e}\"");
            }
        }

        private unsafe short[] Resample(short[] samples, uint sampleRate, uint targetSampleRate, uint channels)
        {
            fixed (short* vals = samples)
            {
                var length = Resample16Bit(vals, null, sampleRate, targetSampleRate, (ulong) samples.LongLength,
                    channels);
                short[] alloc = new short[length];
                fixed (short* output = alloc)
                {
                    Resample16Bit(vals, output, sampleRate, targetSampleRate, (ulong) samples.LongLength, channels);
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
            PcmBytes.NormalizeVolume();
            PcmBytes = PcmBytes.TrimEnd();
            var stream = new BinaryWriter(File.Open(location, FileMode.Create));
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
        }

        public MemoryStream WriteAsWavStream()
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
        }

        private void AddWavHeader(BinaryWriter writer)
        {
            writer.Write(new[] {'R', 'I', 'F', 'F'}); // RIFF Chunk Descriptor
            writer.Write(4 + 8 + 16 + 8 + PcmBytes.Length * 2); // Sub Chunk 1 Size
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
            writer.Write(PcmBytes.Length * Channels * 2); // Sub Chunk 2 Size.
        }
    }
}
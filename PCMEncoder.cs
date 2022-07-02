using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ThirtyDollarWebsiteConverter
{
    public class PcmEncoder
    {
        private const uint SampleRate = 48000; //Hz
        private const int Channels = 1;
        private List<short> PcmBytes { get; } = new();
        public Composition? Composition { get; init; }

        public static ulong TimesExec { get; set; }

        private void AddOrChangeByte(short pcmByte, int index)
        {
            lock (PcmBytes)
            {
                if (index < PcmBytes.Count)
                {
                    float a = pcmByte;
                    float b = PcmBytes[index];
                    float m;

                    a += 32768f;
                    b += 32768f;

                    if (a < 32768f || b < 32768f)
                        m = a * b / 32768f;
                    else
                        m = 2 * (a + b) - a * b / 32768f - 65536f;
                    if (m >= 65535f) m = 65535f;
                    m -= 32768f;

                    PcmBytes[index] = (short) m;

                    //PcmBytes[index] = (short) ((pcmByte + PcmBytes[index]) / 2);
                    return;
                }

                if (index >= PcmBytes.Count) FillWithZeros(index);
                PcmBytes[index] = pcmByte;
            }
        }

        private void FillWithZeros(int index)
        {
            lock (PcmBytes)
            {
                while (index >= PcmBytes.Count) PcmBytes.Add(0);
            }
        }

        public void Start()
        {
            if (Composition == null) throw new Exception("Null Composition");
            var bpm = 300.0;
            double placement = 0;
            var count = Composition.Events.Count;
            double volume = 100;
            foreach (var ev in Composition.Events) //Quick pass for volume
            {
                switch (ev.SoundEvent)
                {
                    case SoundEvent.Volume:
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
            
            Composition.Events.RemoveAll(e => e.SoundEvent == SoundEvent.Volume);
            for (var i = 0; i < Composition!.Events.Count; i++)
            {
                var ev = Composition.Events[i];
                try
                {
                    switch (ev.SoundEvent)
                    {
                        case SoundEvent.Speed:
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
                            count--;
                            Console.WriteLine($"BPM is now: {bpm}");
                            continue;

                        case SoundEvent.GoToLoop:
                            count--;
                            if (ev.Value <= 0) continue;
                            ev.Value--;
                            for (var j = i; j > 0; j--)
                            {
                                if (Composition.Events[j].SoundEvent != SoundEvent.LoopTarget) continue;
                                i = j - 1;
                            }

                            Console.WriteLine($"Going to element: ({i}) - {Composition.Events[i]}");
                            continue;

                        case SoundEvent.JumpToTarget:
                            if (ev.Loop == 0) continue;
                            ev.Loop--;
                            //i = Triggers[(int) ev.Value - 1] - 1;
                            i = Composition.Events.IndexOf(Composition.Events.First(r =>
                                r.SoundEvent == SoundEvent.SetTarget && (int) r.Value == (int) ev.Value)) - 1;
                            Console.WriteLine($"Jumping to element: ({i}) - {Composition.Events[i]}");
                            count--;
                            continue;

                        case SoundEvent.Pause:
                            placement += 1;
                            count--;
                            continue;
                        
                        case SoundEvent.CutAllSounds or SoundEvent.None or SoundEvent.LoopTarget or SoundEvent.SetTarget or SoundEvent.Volume:
                            count--;
                            continue;
                    }

                    List<Event> processAtTheSameTime = new() {Composition.Events[i]};
                    while (i < Composition.Events.Count - 1 &&
                           Composition.Events[i + 1].SoundEvent == SoundEvent.Combine)
                    {
                        processAtTheSameTime.Add(Composition.Events[i + 2]);
                        i += 2;
                    }

                    var breakEarly = i + count < Composition?.Events.Count &&
                                     Composition?.Events[i + 1].SoundEvent == SoundEvent.CutAllSounds;

                    count -= processAtTheSameTime.Count;
                    var median = SampleRate / (bpm / 60);
                    var scale = (int) (median * placement);
                    var index = scale + scale % 2;
                    Console.WriteLine(
                        $"Processing Events: [{scale}] - ({placement} - {count}) \"{processAtTheSameTime.ListElements()}\"");
                    //Console.ReadLine();
                    HandleProcessing(processAtTheSameTime, index, breakEarly ? (int) median : -1);
                    placement++;
                    if (ev.Loop > 1)
                    {
                        ev.Loop--;
                        i--;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private class ProcessedSample
        {
            public short[]? SampleData { get; init; }
            public int ProcessedChunks { get; set; }
            public int SampleLength => SampleData?.Length ?? 0;
            public double Volume { get; init; }
        }

        private void HandleProcessing(IReadOnlyList<Event> events, int index, int breakAtIndex)
        {
            try
            {
                var biggest = events.Select(ev => ev.SampleLength).Prepend(0).Max();

                List<ProcessedSample> samples = new();

                foreach (var ev in events)
                {
                    if (ev.Value == 0)
                    {
                        samples.Add(new ProcessedSample
                        {
                            SampleData = Program.Samples[ev.SampleId],
                            Volume = ev.Volume
                        });
                        continue;
                    }

                    var scale = Math.Pow(2, ev.Value / 12);
                    uint targetRate = (uint) (SampleRate / scale);
                    //Console.WriteLine($"Processing: {ev.SampleId}, {ev.SampleLength}, {++TimesExec}, {ev.Value}, {ev.ValueTimes}");
                    var sampleData = Resample(Program.Samples[ev.SampleId], SampleRate, targetRate, Channels);
                    samples.Add(new ProcessedSample
                    {
                        SampleData = sampleData,
                        Volume = ev.Volume
                    });
                }

                for (var i = 0; i < biggest; i++)
                {
                    var el = samples.Where(q => q.ProcessedChunks < q.SampleLength).ToList();
                    float final = 0;

                    foreach (var r in el)
                    {
                        if (el.Count == 1)
                        {
                            final = (float) ((r.SampleData?[r.ProcessedChunks] ?? 0) * (r.Volume * 0.5 / 100));
                            continue;
                        }
                        
                        var a = final;
                        var b = (float) (r.SampleData?[r.ProcessedChunks] * (r.Volume * 0.5 / 100) ?? 0);
                        float m;

                        a += 32768f;
                        b += 32768f;

                        if (a < 32768f || b < 32768f)
                            m = a * b / 32768f;
                        else
                            m = 2 * (a + b) - a * b / 32768f - 65536f;
                        if (m >= 65535f) m = 65535f;
                        m -= 32768f;
                        final = m;
                    }

                    if (breakAtIndex != -1 && i == breakAtIndex) return;
                    AddOrChangeByte((short) final, index + i);
                    foreach (var ev in samples) ev.ProcessedChunks++;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private unsafe short[] Resample(short[] samples, uint sampleRate, uint targetSampleRate, uint channels)
        {
            fixed (short* vals = samples)
            {
                var length = Resample16B(vals, null, sampleRate, targetSampleRate, (ulong) samples.LongLength,
                    channels);
                short[] alloc = new short[length];
                fixed (short* output = alloc)
                {
                    Resample16B(vals, output, sampleRate, targetSampleRate, (ulong) samples.LongLength, channels);
                }

                return alloc;
            }
        }

        // Original Source: https://github.com/cpuimage/resampler
        private unsafe ulong Resample16B(short* input, short* output, uint inSampleRate, uint outSampleRate,
            ulong inputSize, uint channels)
        {
            var outputSize = (ulong) (inputSize * (double) outSampleRate / inSampleRate);
            outputSize -= outputSize % channels;
            if (output == null)
                return outputSize;
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

        public void Play(int num)
        {
            /*var prg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/mpv",
                    Arguments = "--player-operation-mode=pseudo-gui -",
                    RedirectStandardInput = true,
                    UseShellExecute = false
                }
            };
            prg.Start();

            for (var i = 0; i < PcmBytes.Count; i++)
            {
                await prg.StandardInput.WriteAsync((char) PcmBytes[i]);
                
                while (i + 1 == PcmBytes.Count && !Exited)
                {
                    await Task.Delay(4);
                }
            }
            
            await prg.WaitForExitAsync();*/

            var stream = new BinaryWriter(File.Open($"./out-{num}.wav", FileMode.Create));
            AddWavHeader(stream);
            stream.Write((short) 0);
            foreach (var data in PcmBytes) stream.Write(data);
            stream.Close();
        }

        private void AddWavHeader(BinaryWriter writer)
        {
            writer.Write(new[]{'R','I','F','F'}); // RIFF Chunk Descriptor
            writer.Write(4 + 8 + 16 + 8 + PcmBytes.Count * 2); // Sub Chunk 1 Size
            //Chunk Size 4 bytes.
            writer.Write(new[]{'W','A','V','E'});
            // fmt sub-chunk
            writer.Write(new[]{'f','m','t',' '});
            writer.Write(16); // Sub Chunk 1 Size
            writer.Write((short) 1); // Audio Format 1 = PCM
            writer.Write((short) Channels); // Audio Channels
            writer.Write(SampleRate); // Sample Rate
            writer.Write(SampleRate * Channels * 2 /* Bytes */); // Byte Rate
            writer.Write((short) (Channels * 2)); // Block Align
            writer.Write((short) 16); // Bits per Sample
            // data sub-chunk
            writer.Write(new[]{'d','a','t','a'});
            writer.Write(PcmBytes.Count * Channels * 2); // Sub Chunk 2 Size.
            
        }
    }
}
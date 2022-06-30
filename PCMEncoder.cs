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
                    int a = pcmByte;
                    int b = PcmBytes[index];
                    int m;

                    a += 32768;
                    b += 32768;

                    if (a < 32768 || b < 32768)
                        m = a * b / 32768;
                    else
                        m = 2 * (a + b) - a * b / 32768 - 65536;
                    if (m == 65536) m = 65535;
                    m -= 32768;

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
            var bpm = 300.0;
            ulong placement = 1;
            var count = Composition?.Events.Count ?? 0;
            double volume = 100;
            for (var i = 0; i < Composition?.Events.Count; i++)
            {
                var ev = Composition.Events[i];
                try
                {
                    switch (ev.SoundEvent)
                    {
                        case SoundEvent.Speed:
                            if (ev.ValueTimes) bpm *= ev.Value;
                            else bpm = ev.Value;
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

                        case SoundEvent.Volume:
                            if (ev.ValueTimes) volume *= ev.Value;
                            else volume = ev.Value;
                            continue;
                        
                        case SoundEvent.CutAllSounds or SoundEvent.None or SoundEvent.LoopTarget or SoundEvent.SetTarget:
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
                    placement++;
                    var median = SampleRate / (bpm / 60);
                    var scale = (int) (median * placement);

                    var index = scale - scale % 2;
                    Console.WriteLine(
                        $"Processing Events: [{scale}] - ({placement} - {count}) \"{processAtTheSameTime.ListElements()}\"");
                    //Console.ReadLine();
                    HandleProcessing(processAtTheSameTime, index, breakEarly ? (int) median : -1, volume);
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
        }

        private void HandleProcessing(IReadOnlyList<Event> events, int index, int breakAtIndex, double volume)
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
                            SampleData = Program.Samples[ev.SampleId]
                        });
                        continue;
                    }

                    uint targetRate;
                    // Speed Math
                    if (ev.Value > 0)
                    {
                        var tmp = ev.Value;
                        var dev2 = SampleRate / 2;
                        while (tmp > 12)
                        {
                            dev2 /= 2;
                            tmp -= 12;
                        }
                        targetRate = (uint) (SampleRate - dev2 * (tmp / 12));
                    }
                    else
                    {
                        var tmp = ev.Value;
                        var by2 = SampleRate * 2;
                        while (tmp < -12)
                        {
                            by2 *= 2;
                            tmp += 12;
                        }
                        tmp = Math.Abs(ev.Value);
                        if (tmp == 0) targetRate = by2;
                        else targetRate = (uint) (SampleRate + by2 * 0.5 * (tmp * 0.083D));
                    }
                    //Console.WriteLine($"Processing: {ev.SampleId}, {ev.SampleLength}, {++TimesExec}, {ev.Value}, {ev.ValueTimes}");
                    var sampleData = Resample(Program.Samples[ev.SampleId], SampleRate, targetRate, Channels);
                    samples.Add(new ProcessedSample
                    {
                        SampleData = sampleData
                    });
                }

                for (var i = 0; i < biggest; i++)
                {
                    var el = samples.Where(q => q.ProcessedChunks < q.SampleLength).ToList();
                    var final = 0;

                    foreach (var r in el)
                    {
                        if (el.Count == 1)
                        {
                            final = r.SampleData?[r.ProcessedChunks] ?? 0;
                            continue;
                        }

                        var a = final;
                        int b = r.SampleData?[r.ProcessedChunks] ?? 0;
                        int m;

                        a += 32768;
                        b += 32768;

                        if (a < 32768 || b < 32768)
                            m = a * b / 32768;
                        else
                            m = 2 * (a + b) - a * b / 32768 - 65536;
                        if (m == 65536) m = 65535;
                        m -= 32768;
                        final = m;
                    }

                    if (breakAtIndex != -1 && i == breakAtIndex) return;
                    AddOrChangeByte((short) (final * volume / 100), index + i);
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
                short[] alloc = new short[(int) length];
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
            foreach (var data in PcmBytes) stream.Write(data);
            stream.Close();
        }
    }
}
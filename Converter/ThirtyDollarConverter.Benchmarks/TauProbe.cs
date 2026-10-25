using System.Diagnostics;
using ThirtyDollarConverter.Encoder.Mixers;
using ThirtyDollarConverter.Encoder.Resamplers;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Voices;

namespace ThirtyDollarConverter.Benchmarks;

// ponytail: throwaway - per-phase wall/CPU of the CLI export path; scale it with taskset + DOTNET_PROCESSOR_COUNT.
public static class TauProbe
{
    private static readonly Process Self = Process.GetCurrentProcess();
    private static volatile string _phase = "load";
    private static readonly Dictionary<string, double> Peak = [];

    private static double Cpu()
    {
        Self.Refresh();
        return Self.TotalProcessorTime.TotalMilliseconds;
    }

    public static async Task Run(string path)
    {
        var holder = Workbench.Samples();
        var settings = new EncoderSettings
        {
            SampleRate = 48000, Channels = 2, CutFadeLengthMs = 10,
            Resampler = new HermiteResampler(), Renderer = RenderMethod.Voice
        };

        var sampler = new Thread(Sample) { IsBackground = true };
        sampler.Start();

        var rows = new List<(string Phase, double Wall, double Cpu, int Gen2, double Pause)>();
        var wall = Stopwatch.StartNew();
        double mark_wall = 0, mark_cpu = Cpu(), mark_pause = GC.GetTotalPauseDuration().TotalMilliseconds;
        var mark_gen2 = GC.CollectionCount(2);

        void Mark(string next)
        {
            var now_wall = wall.Elapsed.TotalMilliseconds;
            var now_cpu = Cpu();
            var now_pause = GC.GetTotalPauseDuration().TotalMilliseconds;
            var now_gen2 = GC.CollectionCount(2);
            rows.Add((_phase, now_wall - mark_wall, now_cpu - mark_cpu, now_gen2 - mark_gen2, now_pause - mark_pause));
            (mark_wall, mark_cpu, mark_pause, mark_gen2) = (now_wall, now_cpu, now_pause, now_gen2);
            _phase = next;
        }

        _phase = "parse";
        var sequence = Sequence.FromString(File.ReadAllText(path));
        Mark("placement");

        var encoder = new PcmEncoder(holder, settings,
            s =>
            {
                if (s.StartsWith("Calculated placement")) Mark("resample");
                else if (s.StartsWith("Built the voice list")) Mark("mix");
            },
            (done, total) =>
            {
                if (done == total && _phase == "resample") Mark("build voices");
            });

        var rendered = await encoder.GetSequenceAudio(sequence);
        Mark("wav write");
        encoder.WriteAsWavFile(Stream.Null, rendered.Audio);
        Mark("done");

        Console.WriteLine($"events {sequence.Events.Length:N0}, placements {rendered.Placement.Length:N0}, " +
                          $"sounds {rendered.ProcessedEvents?.Count:N0}, " +
                          $"length {rendered.Audio.GetLength() / 48000.0:F1} s, peak RSS {Self.PeakWorkingSet64 >> 20} MB");
        Console.WriteLine($"{"phase",-14}{"wall ms",10}{"cpu ms",10}{"avg %",8}{"peak %",8}{"gen2",6}{"GC pause ms",13}");
        foreach (var row in rows)
            Console.WriteLine($"{row.Phase,-14}{row.Wall,10:F0}{row.Cpu,10:F0}{100 * row.Cpu / row.Wall,8:F0}" +
                              $"{Peak.GetValueOrDefault(row.Phase),8:F0}{row.Gen2,6}{row.Pause,13:F0}");
        Console.WriteLine($"{"total",-14}{rows.Sum(r => r.Wall),10:F0}{rows.Sum(r => r.Cpu),10:F0}");

        using var render = await encoder.BuildVoices(rendered.TimedEvents, rendered.ProcessedEvents);
        if (Environment.GetEnvironmentVariable("TAU_STATS") != null) Stats(render.Voices);

        // hyperthreads share L1d (32 KB here); a 4096 slice's stereo destination fills it alone
        foreach (var slice in (Environment.GetEnvironmentVariable("TAU_SLICES") ?? "").Split(',',
                     StringSplitOptions.RemoveEmptyEntries).Select(int.Parse))
        {
            var cpu = Cpu();
            var start = Stopwatch.GetTimestamp();
            VoiceMixer.MixAll(render.Voices, slice);
            var mix = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            Console.WriteLine($"slice {slice,5}: mix {mix,7:F0} ms, cpu {100 * (Cpu() - cpu) / mix,5:F0}%");
        }
    }

    /// <summary>What the mix has to do: voice-samples summed, how much of it is identical stacked voices, and the slice scan overhead.</summary>
    private static void Stats(VoiceList list)
    {
        var voices = ((Voice[])typeof(VoiceList)
            .GetField("_voices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(list)!).AsSpan(0, list.Count);

        long samples = 0, terms = 0, cut = 0;
        var unique = new HashSet<(int, int, int, int, int)>();
        foreach (ref readonly var v in voices)
        {
            long length = v.End - v.Start;
            samples += length;
            terms += length * ((v.LeftFromLeft != 0 ? 1 : 0) + (v.RightFromRight != 0 ? 1 : 0) +
                               (v.LeftFromRight != 0 ? 1 : 0) + (v.RightFromLeft != 0 ? 1 : 0));
            if (v.CutAt != int.MaxValue) cut++;
            unique.Add((v.Start, v.End, v.CutAt, v.SourceOffset, v.Sound));
        }

        // voices each 4096-sample slice walks past (from LowerBound(from - Longest) to its end) vs. ones it mixes
        long walked = 0, mixed = 0;
        var starts = new int[voices.Length];
        for (var i = 0; i < starts.Length; i++) starts[i] = voices[i].Start;
        for (var from = 0; from < list.Length; from += VoiceMixer.SliceLength)
        {
            var to = Math.Min(from + VoiceMixer.SliceLength, list.Length);
            var low = LowerBound(starts, (long)from - list.Longest);
            var high = LowerBound(starts, to);
            walked += high - low;
            for (var i = low; i < high; i++)
                if (voices[i].End > from) mixed++;
        }

        Console.WriteLine($"\nvoices {list.Count:N0}, cut {cut:N0}, longest {list.Longest / 48000.0:F2} s, " +
                          $"mean {samples / (double)list.Count / 48000.0:F3} s");
        Console.WriteLine($"voice-samples {samples:N0} ({samples / (double)list.Length:F0} voices sounding on average), " +
                          $"gain terms {terms:N0}");
        Console.WriteLine($"distinct (start, end, cut, offset, sound) {unique.Count:N0} " +
                          $"= {100.0 * unique.Count / list.Count:F1}% of voices");
        Console.WriteLine($"slice walk: {walked:N0} visited, {mixed:N0} mixed ({100.0 * mixed / walked:F1}%)");
    }

    /// <summary>
    ///     The playback feeder's side: one <see cref="VoiceCursor" /> mixing the song a block at a
    ///     time on one thread, as VoicePlayback does, timed per block (render + interleave).
    ///     TAU_BLOCKS picks the block sizes, TAU_WINDOW=from,seconds narrows the walk.
    /// </summary>
    public static async Task Feeder(string path)
    {
        var settings = new EncoderSettings
        {
            SampleRate = 48000, Channels = 2, CutFadeLengthMs = 10,
            Resampler = new HermiteResampler(), Renderer = RenderMethod.Voice
        };
        var encoder = new PcmEncoder(Workbench.Samples(), settings);
        var sequence = Sequence.FromString(File.ReadAllText(path));
        var build = Stopwatch.StartNew();
        using var render = await encoder.BuildVoices([sequence]);
        var list = render.Voices;
        Console.WriteLine($"voices {list.Count:N0}, length {list.Length / 48000.0:F1} s, " +
                          $"prepare + build {build.Elapsed.TotalSeconds:F1} s, peak RSS {Self.PeakWorkingSet64 >> 20} MB");

        var window = (Environment.GetEnvironmentVariable("TAU_WINDOW") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(double.Parse).ToArray();
        var from = window.Length > 0 ? (int)(window[0] * 48000) : 0;
        var to = window.Length > 1 ? Math.Min(list.Length, from + (int)(window[1] * 48000)) : list.Length;

        Console.WriteLine($"{"walk",-12}{"block",6}{"mean",7}{"cpu",7}{"p0.25s",8}{"p1s",7}{"p10s",7}" +
                          $"{"dense60 @s",11}{"stall 10s",11}{"stall 30s",11}{"seek 10s",10}{"seek 30s",10}" +
                          $"{"seek stall%",12}{"max delta",11}");

        // TAU_PAR=voice,time and TAU_WORKERS=2,4,7 add the parallel walks, checked against the serial one
        var modes = (Environment.GetEnvironmentVariable("TAU_PAR") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);
        var workers = (Environment.GetEnvironmentVariable("TAU_WORKERS") ?? "7").Split(',').Select(int.Parse)
            .ToArray();
        foreach (var block in (Environment.GetEnvironmentVariable("TAU_BLOCKS") ?? "1024").Split(',')
                     .Select(int.Parse))
        {
            var reference = modes.Length > 0 ? new float[2 * (to - from)] : null;
            var cpu = Cpu();
            var costs = Walk(list, block, from, to, reference);
            Report("serial", costs, block, from, Cpu() - cpu, 0);

            foreach (var mode in modes)
            foreach (var count in workers)
            {
                cpu = Cpu();
                var (parallel, delta) = WalkParallel(list, block, from, to, count, mode == "voice", reference!);
                Report($"{mode} x{count}", parallel, block, from, Cpu() - cpu, delta);
            }
        }
    }

    /// <summary>
    ///     One block spread over <paramref name="workers" /> threads, two ways. By voice: each
    ///     worker mixes its share of the sounding voices into buffers of its own and the
    ///     coordinator sums them - the sum's order changes, so it isn't bit-identical. By time:
    ///     each worker walks every sounding voice but mixes only its slice of the block, straight
    ///     into the output - bit-identical. The coordinator keeps the active set, as the cursor
    ///     does, and times each block whole.
    /// </summary>
    private static (double[] Costs, float MaxDelta) WalkParallel(VoiceList list, int block, int from, int to,
        int workers, bool byVoice, float[] reference)
    {
        var active = new int[list.Count];
        var count = 0;
        var next = list.LowerBound((long)from - list.Longest);
        {
            var starts = list.Starts;
            var voices = list.Voices;
            for (; next < starts.Length && starts[next] < from; next++)
                if (voices[next].End > from)
                    active[count++] = next;
        }

        var left = new float[block];
        var right = new float[block];
        var interleaved = new float[2 * block];
        var own_left = new float[workers][];
        var own_right = new float[workers][];
        for (var k = 0; k < workers; k++)
        {
            own_left[k] = new float[block];
            own_right[k] = new float[block];
        }

        int b0 = 0, b1 = 0;
        var stop = false;
        using var barrier = new Barrier(workers + 1);
        for (var k = 0; k < workers; k++)
        {
            var worker = k;
            new Thread(() =>
            {
                while (true)
                {
                    barrier.SignalAndWait();
                    if (stop) return;

                    var voices = list.Voices;
                    var length = b1 - b0;
                    if (byVoice)
                    {
                        var l = own_left[worker].AsSpan(0, length);
                        var r = own_right[worker].AsSpan(0, length);
                        l.Clear();
                        r.Clear();
                        for (var a = worker * count / workers; a < (worker + 1) * count / workers; a++)
                        {
                            var index = active[a];
                            ref readonly var voice = ref voices[index];
                            if (voice.End <= b0) continue;
                            list.MixRange(index, Math.Max(b0, voice.Start), Math.Min(b1, voice.End), b0, l, r);
                        }
                    }
                    else
                    {
                        // slices start on 16 floats, so no two workers write one cache line
                        var lo = b0 + ((worker * length / workers) & ~15);
                        var hi = worker == workers - 1 ? b1 : b0 + (((worker + 1) * length / workers) & ~15);
                        left.AsSpan(lo - b0, hi - lo).Clear();
                        right.AsSpan(lo - b0, hi - lo).Clear();
                        for (var a = 0; a < count; a++)
                        {
                            var index = active[a];
                            ref readonly var voice = ref voices[index];
                            var s = Math.Max(lo, voice.Start);
                            var e = Math.Min(hi, voice.End);
                            if (s < e) list.MixRange(index, s, e, b0, left, right);
                        }
                    }

                    barrier.SignalAndWait();
                }
            }) { IsBackground = true }.Start();
        }

        var costs = new double[(to - from + block - 1) / block];
        var max_delta = 0f;
        for (var b = 0; b < costs.Length; b++)
        {
            var start = Stopwatch.GetTimestamp();
            b0 = from + b * block;
            b1 = Math.Min(b0 + block, to);
            var frames = b1 - b0;
            var starts = list.Starts;
            var voices = list.Voices;
            while (next < starts.Length && starts[next] < b1) active[count++] = next++;

            barrier.SignalAndWait(); // go
            barrier.SignalAndWait(); // done

            if (byVoice)
            {
                left.AsSpan(0, frames).Clear();
                right.AsSpan(0, frames).Clear();
                for (var k = 0; k < workers; k++)
                {
                    SampleMixer.Mix(own_left[k].AsSpan(0, frames), left.AsSpan(0, frames), 1f);
                    SampleMixer.Mix(own_right[k].AsSpan(0, frames), right.AsSpan(0, frames), 1f);
                }
            }

            var kept = 0;
            for (var a = 0; a < count; a++)
                if (voices[active[a]].End > b1)
                    active[kept++] = active[a];
            count = kept;

            for (var i = 0; i < frames; i++)
            {
                interleaved[2 * i] = left[i];
                interleaved[2 * i + 1] = right[i];
            }

            costs[b] = Stopwatch.GetElapsedTime(start).TotalSeconds;

            var at = 2 * (b0 - from);
            for (var i = 0; i < 2 * frames; i++)
                max_delta = Math.Max(max_delta, Math.Abs(interleaved[i] - reference[at + i]));
        }

        stop = true;
        barrier.SignalAndWait();
        return (costs, max_delta);
    }

    private static double[] Walk(VoiceList list, int block, int from, int to, float[]? keep)
    {
        using var cursor = new VoiceCursor();
        cursor.Attach(list, from);
        var left = new float[block];
        var right = new float[block];
        var interleaved = new float[2 * block];
        var costs = new double[(to - from + block - 1) / block];

        for (var b = 0; b < costs.Length; b++)
        {
            var frames = Math.Min(block, to - cursor.Position);
            var start = Stopwatch.GetTimestamp();
            cursor.Render(left.AsSpan(0, frames), right.AsSpan(0, frames));
            for (var i = 0; i < frames; i++)
            {
                interleaved[2 * i] = left[i];
                interleaved[2 * i + 1] = right[i];
            }

            costs[b] = Stopwatch.GetElapsedTime(start).TotalSeconds;
            if (keep != null) interleaved.AsSpan(0, 2 * frames).CopyTo(keep.AsSpan(2 * b * block));
        }

        cursor.Attach(null, 0);
        return costs;
    }

    /// <summary>
    ///     Loads as the meter shows them (cost over audio length, per window), the densest minute,
    ///     and the queue simulated through the walk: from the walk's start with 10 s / 30 s of
    ///     render-ahead, and from a seek to every second of it (the worst one, and the share of
    ///     seek points that stall at all).
    /// </summary>
    private static void Report(string walk, double[] costs, int block, int from, double cpuMs, float maxDelta)
    {
        var d = block / 48000.0;
        var total = costs.Sum();

        double Peak(double seconds)
        {
            var n = Math.Max(1, (int)Math.Round(seconds / d));
            double sum = 0, peak = 0;
            for (var i = 0; i < costs.Length; i++)
            {
                sum += costs[i];
                if (i >= n) sum -= costs[i - n];
                if (i >= n - 1) peak = Math.Max(peak, sum / (n * d));
            }

            return peak;
        }

        var minute = Math.Min(costs.Length, (int)(60 / d));
        double run = 0, best = 0;
        var best_at = 0;
        for (var i = 0; i < costs.Length; i++)
        {
            run += costs[i];
            if (i >= minute) run -= costs[i - minute];
            if (i >= minute - 1 && run > best) (best, best_at) = (run, i - minute + 1);
        }

        // stalled seconds playing from block `start` to the end with `ahead` seconds of render-ahead
        double Stall(int start, double ahead)
        {
            double queue = 0, stall = 0;
            for (var i = start; i < costs.Length; i++)
            {
                queue = Math.Min(queue, ahead) - costs[i];
                if (queue < 0)
                {
                    stall -= queue;
                    queue = 0;
                }

                queue += d;
            }

            return stall;
        }

        double worst10 = 0, worst30 = 0;
        int seeks = 0, stalled = 0;
        for (var start = 0; start < costs.Length; start += Math.Max(1, (int)(1 / d)), seeks++)
        {
            var s10 = Stall(start, 10);
            worst10 = Math.Max(worst10, s10);
            worst30 = Math.Max(worst30, Stall(start, 30));
            if (s10 > 0.001) stalled++;
        }

        Console.WriteLine($"{walk,-12}{block,6}{100 * total / (costs.Length * d),6:F0}%" +
                          $"{100 * cpuMs / 1000 / (costs.Length * d),6:F0}%{100 * Peak(0.25),7:F0}%" +
                          $"{100 * Peak(1),6:F0}%{100 * Peak(10),6:F0}%{from / 48000.0 + best_at * d,11:F0}" +
                          $"{Stall(0, 10),10:F1}s{Stall(0, 30),10:F1}s{worst10,9:F1}s{worst30,9:F1}s" +
                          $"{100.0 * stalled / seeks,11:F0}%{maxDelta,11:G3}");
    }

    private static int LowerBound(int[] starts, long sample)
    {
        int low = 0, high = starts.Length;
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            if (starts[mid] < sample) low = mid + 1;
            else high = mid;
        }

        return low;
    }

    private static void Sample()
    {
        var last_cpu = Cpu();
        var last = Stopwatch.GetTimestamp();
        while (true)
        {
            Thread.Sleep(250);
            var cpu = Cpu();
            var now = Stopwatch.GetTimestamp();
            var percent = 100 * (cpu - last_cpu) / Stopwatch.GetElapsedTime(last, now).TotalMilliseconds;
            var phase = _phase;
            lock (Peak) Peak[phase] = Math.Max(Peak.GetValueOrDefault(phase), percent);
            (last_cpu, last) = (cpu, now);
        }
    }
}

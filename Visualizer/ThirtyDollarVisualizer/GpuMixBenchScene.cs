using System.Diagnostics;
using System.Globalization;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Audio.Gpu;
using Sundex.Engine;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Voices;

namespace ThirtyDollarVisualizer;

/// <summary>
///     The GPU voice mixer's go/no-go (docs/handover/gpu-voice-mixer-plan.md, step 1). Started with
///     <c>--mode gpu-mix-bench -i &lt;song&gt;</c>: builds the song's voices as playback would,
///     uploads them, and mixes every block on the GPU back to back - no audio, no frame budget -
///     timing each with GPU timestamps. Then it checks the mix against
///     <see cref="VoiceMixer.MixAll" />, logs <c>[GpuMixBench]</c> lines ending in a verdict, and
///     closes the window.
///     <para>
///         <c>GPU_MIX_BLOCK</c> (default 4096 frames), <c>GPU_MIX_CHUNK</c> (1024 voices) and
///         <c>GPU_MIX_WINDOW=from,seconds</c> tune a run.
///     </para>
/// </summary>
public sealed class GpuMixBenchScene(Game game, ThirtyDollarWorkflow workflow, string? sequence) : Scene(game)
{
    /// <summary>Go if the GPU's 10 s peak stays under this share of real time (Kris, 2026-11-01: 85%)...</summary>
    private const double GatePeak10S = 0.85;

    /// <summary>...and its mix is within this many dB of <see cref="VoiceMixer.MixAll" />'s peak, or of an exact mix's.</summary>
    private const double GateDeltaDb = -100;

    /// <summary>Blocks submitted per fence: also the output ring's slot count.</summary>
    private const int Batch = 16;

    private readonly VoiceCursor _cursor = new();
    private readonly int[] _queries = new int[2 * Batch];
    private readonly Stopwatch _wall = new();

    private double[] _advance = [];
    private int[] _activeCounts = [];
    private int _batchFirst, _batchCount;
    private int _block, _from, _to, _blocks, _next;
    private Task<VoiceRender>? _build;
    private bool _done;
    private GLSync _fence;
    private double[] _gpu = [];
    private GpuVoiceKernels? _kernels;
    private float[] _mixed = [];
    private Task<(float[] Left, float[] Right, double Seconds)>? _reference;
    private string _renderer = "";
    private VoiceRender? _render;
    private bool _supported;

    private static int Env(string name, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;
    }

    public override void Initialize(InitArguments initArguments)
    {
        _renderer = initArguments.GLInfo.Renderer;
        _supported = GpuVoiceKernels.IsSupported(initArguments.GLInfo);
    }

    public override void Start()
    {
    }

    public override void TransitionedTo()
    {
        if (_build != null) return;

        if (sequence == null)
        {
            Finish("error", "no song: pass -i <song>");
            return;
        }

        _wall.Start();
        var encoder = new PcmEncoder(workflow.SampleHolder, workflow.EncoderSettings);
        _build = Task.Run(() => encoder.BuildVoices([Sequence.FromString(File.ReadAllText(sequence))]));
    }

    public override void Update(UpdateArguments updateArgs)
    {
        if (_done)
        {
            Game.Close();
            return;
        }

        if (_build is not { IsCompleted: true }) return;

        try
        {
            if (_kernels == null) Prepare();
            else if (_fence != default) Collect();
            else if (_next < _blocks) Submit();
            else if (_reference!.IsCompleted) Verify();
        }
        catch (Exception e)
        {
            Logger.Error(e, "[GpuMixBench] failed");
            Finish("error", e.Message);
        }
    }

    private void Prepare()
    {
        if (!_supported)
        {
            Finish("unsupported", "the context has no compute shaders or storage buffers");
            return;
        }

        _render = _build!.Result;
        var list = _render.Voices;
        if (list.Count == 0)
        {
            Finish("error", "the song has no audible voices");
            return;
        }

        var build_seconds = _wall.Elapsed.TotalSeconds;
        var rate = (int)workflow.EncoderSettings.SampleRate;

        _block = Env("GPU_MIX_BLOCK", 4096);
        var window = (Environment.GetEnvironmentVariable("GPU_MIX_WINDOW") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
        _from = window.Length > 0 ? Math.Clamp((int)(window[0] * rate), 0, list.Length) : 0;
        _to = window.Length > 1 ? Math.Min(list.Length, _from + (int)(window[1] * rate)) : list.Length;
        _blocks = (_to - _from + _block - 1) / _block;
        _gpu = new double[_blocks];
        _advance = new double[_blocks];
        _activeCounts = new int[_blocks];
        _mixed = new float[2 * (_to - _from)];

        // The reference runs on the thread pool while the GPU works: MixAll for the whole song,
        // a cursor walk - bit-identical to it - for a window.
        var (from, to) = (_from, _to);
        _reference = Task.Run(() =>
        {
            var start = Stopwatch.GetTimestamp();
            float[] left, right;
            if (from == 0 && to == list.Length)
            {
                var audio = VoiceMixer.MixAll(list);
                left = audio.Samples[0];
                right = audio.Samples.Length > 1 ? audio.Samples[1] : left;
            }
            else
            {
                using var cursor = new VoiceCursor();
                cursor.Attach(list, from);
                left = new float[to - from];
                right = new float[to - from];
                for (var at = from; at < to; at += 4096)
                {
                    var frames = Math.Min(4096, to - at);
                    cursor.Render(left.AsSpan(at - from, frames), right.AsSpan(at - from, frames));
                }

                cursor.Attach(null, 0);
                var offset = new float[list.Length];
                left.CopyTo(offset, from);
                left = offset;
                offset = new float[list.Length];
                right.CopyTo(offset, from);
                right = offset;
            }

            return (left, right, Stopwatch.GetElapsedTime(start).TotalSeconds);
        });

        _kernels = new GpuVoiceKernels(Env("GPU_MIX_CHUNK", 1024));
        var upload = Stopwatch.GetTimestamp();
        if (!_kernels.SetList(list))
        {
            Finish("unsupported", "the song's voices or sounds don't fit in a storage buffer");
            return;
        }

        _kernels.UploadVoices(long.MaxValue);
        _kernels.EnsureOutput(Batch, _block);
        GL.Finish();
        var upload_ms = Stopwatch.GetElapsedTime(upload).TotalMilliseconds;

        for (var i = 0; i < _queries.Length; i++) _queries[i] = GL.GenQuery();
        _cursor.Attach(list, _from);

        Logger.Information(
            "[GpuMixBench] song=\"{Song:l}\" voices={Voices} length={Length:0.0}s sounds={Sounds} " +
            "sample_mb={SampleMb:0.0} voice_mb={VoiceMb:0.0} build={Build:0.0}s upload={Upload:0}ms " +
            "window={From:0.0}s+{Seconds:0.0}s block={Block} chunk={Chunk} renderer=\"{Renderer:l}\"",
            Path.GetFileName(sequence), list.Count, list.Length / (double)rate, list.Sounds.Length,
            _kernels.SampleBytes / 1048576.0, list.Count * 36 / 1048576.0, build_seconds, upload_ms,
            _from / (double)rate, (_to - _from) / (double)rate, _block, _kernels.ChunkVoices, _renderer);
    }

    /// <summary>Queues up to <see cref="Batch" /> blocks, each between two timestamps, then a fence.</summary>
    private void Submit()
    {
        var kernels = _kernels!;
        _batchFirst = _next;
        _batchCount = Math.Min(Batch, _blocks - _next);

        for (var k = 0; k < _batchCount; k++, _next++)
        {
            var start = _from + _next * _block;
            var frames = Math.Min(_block, _to - start);

            var advance = Stopwatch.GetTimestamp();
            var active = _cursor.Advance(frames);
            _advance[_next] = Stopwatch.GetElapsedTime(advance).TotalSeconds;
            _activeCounts[_next] = active.Length;

            var chunks = kernels.Chunks(active.Length);
            GL.QueryCounter(_queries[2 * k], QueryCounterTarget.Timestamp);
            kernels.BeginBlock(active, frames);
            kernels.MixChunks(start, frames, active.Length, 0, chunks);
            kernels.Reduce(frames, chunks, k);
            GL.QueryCounter(_queries[2 * k + 1], QueryCounterTarget.Timestamp);
        }

        _fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
    }

    /// <summary>Waits for the batch - a bench may stall - and keeps its output and timings.</summary>
    private void Collect()
    {
        var status = GL.ClientWaitSync(_fence, SyncObjectMask.SyncFlushCommandsBit, 10_000_000_000);
        if (status is not (SyncStatus.AlreadySignaled or SyncStatus.ConditionSatisfied))
            throw new TimeoutException($"A batch of blocks didn't finish in 10 s ({status}).");

        GL.DeleteSync(_fence);
        _fence = default;

        for (var k = 0; k < _batchCount; k++)
        {
            var b = _batchFirst + k;
            var start = _from + b * _block;
            var frames = Math.Min(_block, _to - start);
            _kernels!.ReadOutput(k, _mixed.AsSpan(2 * (start - _from), 2 * frames));

            GL.GetQueryObjecti64(_queries[2 * k], QueryObjectParameterName.QueryResult, out var begin);
            GL.GetQueryObjecti64(_queries[2 * k + 1], QueryObjectParameterName.QueryResult, out var end);
            _gpu[b] = (end - begin) / 1e9;
        }
    }

    private void Verify()
    {
        var (left, right, mix_all_seconds) = _reference!.Result;
        var rate = (int)workflow.EncoderSettings.SampleRate;

        float peak = 0, max_delta = 0;
        var worst = 0;
        for (var i = _from; i < _to; i++)
        {
            peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i])));
            var delta = Math.Max(Math.Abs(_mixed[2 * (i - _from)] - left[i]),
                Math.Abs(_mixed[2 * (i - _from) + 1] - right[i]));
            if (delta <= max_delta) continue;

            max_delta = delta;
            worst = i;
        }

        var delta_db = max_delta > 0 && peak > 0 ? 20 * Math.Log10(max_delta / peak) : double.NegativeInfinity;
        var d = _block / (double)rate;
        var slowest = Array.IndexOf(_gpu, _gpu.Max());

        Logger.Information(
            "[GpuMixBench] gpu mean={Mean:0.0}% p0.25s={P025:0.0}% p1s={P1:0.0}% p10s={P10:0.0}% " +
            "slowest_block={Slowest:0.00}ms at={At:0.0}s active={Active} max_active={MaxActive}",
            100 * _gpu.Sum() / (_blocks * d), 100 * Peak(_gpu, d, 0.25), 100 * Peak(_gpu, d, 1),
            100 * Peak(_gpu, d, 10), 1000 * _gpu[slowest], (_from + slowest * _block) / (double)rate,
            _activeCounts[slowest], _activeCounts.Max());
        Logger.Information(
            "[GpuMixBench] snapshot (feeder CPU) mean={Mean:0.00}% p10s={P10:0.00}% slowest={Slowest:0.00}ms",
            100 * _advance.Sum() / (_blocks * d), 100 * Peak(_advance, d, 10), 1000 * _advance.Max());
        Logger.Information(
            "[GpuMixBench] max_delta={Delta:G3} peak={Peak:G3} delta_db={Db:0.0} at={At:0.000}s mix_all={MixAll:0.0}s",
            max_delta, peak, delta_db, worst / (double)rate, mix_all_seconds);

        var by_active = Array.IndexOf(_activeCounts, _activeCounts.Max());
        Logger.Information("[GpuMixBench] densest block: {Ms:0.00}ms for {Active} voices at={At:0.0}s",
            1000 * _gpu[by_active], _activeCounts[by_active], (_from + by_active * _block) / (double)rate);

        // Is the delta the GPU's error or the reference's? Both against an exact (double) mix of the worst block.
        var block_start = _from + (worst - _from) / _block * _block;
        var exact = Exact(block_start, Math.Min(_block, _to - block_start));
        double cpu_error = 0, gpu_error = 0;
        for (var i = 0; i < exact.Length / 2; i++)
        {
            var at = block_start + i;
            cpu_error = Math.Max(cpu_error, Math.Max(Math.Abs(left[at] - exact[2 * i]), Math.Abs(right[at] - exact[2 * i + 1])));
            gpu_error = Math.Max(gpu_error, Math.Max(Math.Abs(_mixed[2 * (at - _from)] - exact[2 * i]),
                Math.Abs(_mixed[2 * (at - _from) + 1] - exact[2 * i + 1])));
        }

        Logger.Information(
            "[GpuMixBench] exact mix of the worst block at={At:0.000}s: cpu_error={Cpu:G3} ({CpuDb:0.0} dB) " +
            "gpu_error={Gpu:G3} ({GpuDb:0.0} dB)",
            block_start / (double)rate, cpu_error, 20 * Math.Log10(cpu_error / peak), gpu_error,
            20 * Math.Log10(gpu_error / peak));

        // At tau's peak MixAll's own float rounding (one long sum of ~590k voices) is -92 dB, so the
        // accuracy gate also passes when the GPU is that close to the exact mix.
        var exact_db = 20 * Math.Log10(gpu_error / peak);
        var go = Peak(_gpu, d, 10) < GatePeak10S && (delta_db < GateDeltaDb || exact_db < GateDeltaDb);
        Finish(go ? "go" : "no-go", $"gate p10s<{GatePeak10S:P0}, delta or exact error<{GateDeltaDb}dB");
    }

    /// <summary>
    ///     The block at <paramref name="start" /> mixed in double precision, interleaved: the voices'
    ///     terms as <c>MixRange</c> forms them, with nothing rounded until the end.
    /// </summary>
    private double[] Exact(int start, int frames)
    {
        var list = _render!.Voices;
        using var cursor = new VoiceCursor();
        cursor.Attach(list, start);
        var active = cursor.Advance(frames).ToArray();
        cursor.Attach(null, 0);

        var exact = new double[2 * frames];
        Parallel.For(0, (frames + 255) / 256, tile =>
        {
            var voices = list.Voices;
            var sounds = list.Sounds;
            var fade_length = list.FadeLength;
            for (var t = tile * 256; t < Math.Min(frames, tile * 256 + 256); t++)
            {
                var at = start + t;
                double l = 0, r = 0;
                foreach (var index in active)
                {
                    var v = voices[index];
                    if (at < v.Start || at >= v.End) continue;

                    var sound = sounds[v.Sound];
                    var source_left = sound.Samples[0];
                    var source_right = sound.Samples.Length > 1 && sound.SourceChannels > 1 ? sound.Samples[1] : source_left;
                    var i = v.SourceOffset + (at - v.Start);
                    var fade = at < v.CutAt ? 1.0 : (double)(v.CutAt + fade_length - at) / fade_length;
                    l += (source_left[i] * (double)v.LeftFromLeft + source_right[i] * (double)v.LeftFromRight) * fade;
                    r += (source_right[i] * (double)v.RightFromRight + source_left[i] * (double)v.RightFromLeft) * fade;
                }

                exact[2 * t] = l;
                exact[2 * t + 1] = r;
            }
        });

        return exact;
    }

    /// <summary>The highest load over any <paramref name="seconds" /> of blocks: cost over audio length, as TauProbe reports it.</summary>
    private static double Peak(double[] costs, double blockSeconds, double seconds)
    {
        var n = Math.Max(1, (int)Math.Round(seconds / blockSeconds));
        double sum = 0, peak = 0;
        for (var i = 0; i < costs.Length; i++)
        {
            sum += costs[i];
            if (i >= n) sum -= costs[i - n];
            if (i >= Math.Min(n, costs.Length) - 1) peak = Math.Max(peak, sum / (Math.Min(n, i + 1) * blockSeconds));
        }

        return peak;
    }

    private void Finish(string verdict, string detail)
    {
        // nohup hides the exit code from viz.sh, so the verdict is logged as well
        Logger.Information("[GpuMixBench] verdict={Verdict:l} {Detail:l}", verdict, detail);
        Environment.ExitCode = verdict == "go" ? 0 : 1;
        _done = true;
    }

    public override void Render(RenderArguments renderArgs)
    {
    }

    public override void Resize(int w, int h)
    {
    }

    public override void Shutdown()
    {
        _kernels?.Dispose();
        foreach (var query in _queries)
            if (query != 0)
                GL.DeleteQuery(query);
        _cursor.Attach(null, 0);
        _render?.Dispose();
    }

    public override void FileDrop(string[] locations)
    {
    }

    public override void Keyboard(KeyboardState state)
    {
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
    }
}

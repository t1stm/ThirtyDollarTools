using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Benchmarks;

/// <summary>
///     One editor keystroke on a real cover: retune a note in the middle, then re-render.
///     Each invocation edits the state the previous one left behind, which is how the editor
///     actually drives this - hold a scroll wheel over a note and it re-renders per notch.
/// </summary>
[MemoryDiagnoser(false)]
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 3, invocationCount: 1)]
public class CoverEditBenchmarks
{
    private PcmEncoder _encoder = null!;
    private RenderedSequence _rendered = null!;
    private Sequence _sequence = null!;
    private int _step;

    public static IEnumerable<Cover> CoverSource => Covers.All;

    [ParamsSource(nameof(CoverSource))] public Cover Cover { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        _encoder = Workbench.Encoder();
        _sequence = Workbench.LoadSequence(Cover.Path);
        _rendered = _encoder.GetSequenceAudio(_sequence).GetAwaiter().GetResult();
    }

    private void Edit()
    {
        Covers.RetuneMiddleEvent(_sequence, ++_step);
    }

    [Benchmark(Baseline = true)]
    public async Task<int> FullRender()
    {
        Edit();
        var rendered = await _encoder.GetSequenceAudio(_sequence);
        return rendered.Audio.GetLength();
    }

    /// <summary>
    ///     What an edit costs when the render is thrown away and redone, but the resampled samples
    ///     are kept - what the incremental renderer falls back to internally, and the fair
    ///     "just re-render it" baseline. <see cref="FullRender" /> above also pays for resampling
    ///     every sound, which only happens on the first render of a session.
    /// </summary>
    [Benchmark]
    public async Task<int> FullRenderWarm()
    {
        Edit();
        var rendered = await _encoder.GetMultipleSequencesAudio([_sequence], _rendered.ProcessedEvents);
        return rendered.Audio.GetLength();
    }

    [Benchmark]
    public async Task<int> Incremental()
    {
        Edit();
        _rendered = await _encoder.ComputeIncrementalAudio(_rendered, [_sequence]);
        return _rendered.Audio.GetLength();
    }
}
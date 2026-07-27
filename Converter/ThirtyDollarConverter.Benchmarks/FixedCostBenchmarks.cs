using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;

namespace ThirtyDollarConverter.Benchmarks;

/// <summary>
///     What an edit costs before a single sample is rendered. Once the re-rendered range is down
///     to a couple of chunks these dominate, which is why range-overwrite lands closer to a warm
///     full render than the size of its dirty range suggests. Measured on the same project the
///     editor benchmarks use.
/// </summary>
[MemoryDiagnoser(false)]
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 5, invocationCount: 1)]
public class FixedCostBenchmarks
{
    private PlacementCalculator _calculator = null!;
    private EditorEdits _edits = null!;
    private RenderedSequence _rendered = null!;
    private Sequence _sequence = null!;

    [GlobalSetup]
    public void Setup()
    {
        var encoder = Workbench.Encoder();
        _edits = new EditorEdits(Workbench.LoadProject());
        _sequence = _edits.Project.ToSequence();
        _rendered = encoder.GetSequenceAudio(_sequence).GetAwaiter().GetResult();
        _calculator = new PlacementCalculator(new EncoderSettings
        {
            SampleRate = 48000,
            Channels = 2,
            EnableNormalization = false,
            CutFadeLengthMs = 4
        });
    }

    /// <summary>Rebuilding the merged sequence out of the project after an edit.</summary>
    [Benchmark]
    public int BuildSequence()
    {
        return _edits.Project.ToSequence().Events.Length;
    }

    /// <summary>Turning that sequence into sample-indexed placements - needed to diff at all.</summary>
    [Benchmark]
    public int CalculatePlacements()
    {
        return _calculator.CalculateMany([_sequence]).Count();
    }

    /// <summary>Summing every track into the final buffer. Runs over the whole song on every
    /// edit, in both incremental implementations.</summary>
    [Benchmark]
    public int MixDown()
    {
        return _rendered.Mixer!.MixDown().GetLength();
    }
}

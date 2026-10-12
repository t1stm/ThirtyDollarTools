using Sundex.Engine.Renderer.Debug;

namespace Sundex.Components.Tests;

/// <summary>
///     The text benchmark's gate (docs/handover/sundex-slug-plan.md, Phase 2) is a p95, so
///     the percentile maths gets a check of its own. Lives here because this is the nearest
///     project that already links the engine.
/// </summary>
public class GpuTimerPercentileTests
{
    [Fact]
    public void NearestRankOverOneToHundred()
    {
        var samples = Enumerable.Range(1, 100).Select(i => (double)i).Reverse().ToList();

        Assert.Equal(50, GpuTimer.Percentile(samples, 50));
        Assert.Equal(95, GpuTimer.Percentile(samples, 95));
        Assert.Equal(100, GpuTimer.Percentile(samples, 100));
        Assert.Equal(1, GpuTimer.Percentile(samples, 0));
        Assert.Equal(100, samples[0]); // input left in its own order
    }

    [Fact]
    public void P95OfSixHundredIsThe570thSmallest()
    {
        var samples = Enumerable.Range(1, 600).Select(i => i / 10.0).ToList();

        Assert.Equal(57.0, GpuTimer.Percentile(samples, 95));
    }

    [Fact]
    public void OneSampleIsEveryPercentile()
    {
        Assert.Equal(3.5, GpuTimer.Percentile([3.5], 50));
        Assert.Equal(3.5, GpuTimer.Percentile([3.5], 95));
    }
}

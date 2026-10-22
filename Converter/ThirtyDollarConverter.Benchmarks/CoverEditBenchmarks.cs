using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Voices;
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
    private Dictionary<(string, double), ProcessedEvent> _voiceCache = null!;
    private PcmEncoder _voiceEncoder = null!;

    public static IEnumerable<Cover> CoverSource => Covers.All;

    [ParamsSource(nameof(CoverSource))] public Cover Cover { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        _encoder = Workbench.Encoder();
        _sequence = Workbench.LoadSequence(Cover.Path);
        _rendered = _encoder.GetSequenceAudio(_sequence).GetAwaiter().GetResult();

        _voiceEncoder = Workbench.Encoder(RenderMethod.Voice);
        using var voices = _voiceEncoder.BuildVoices([_sequence]).GetAwaiter().GetResult();
        _voiceCache = voices.ProcessedEvents;
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

    /// <summary>
    ///     What an edit costs the streaming player: place, look the resampled sounds up and
    ///     rebuild the voice list. Nothing is mixed ahead - playback mixes as it goes.
    /// </summary>
    [Benchmark]
    public async Task<int> VoiceRebuild()
    {
        Edit();
        using var render = await _voiceEncoder.BuildVoices([_sequence], _voiceCache);
        return render.Voices.Count;
    }

    /// <summary>A whole render through the voice mixer, resampled sounds kept - what an export costs.</summary>
    [Benchmark]
    public async Task<int> VoiceRenderWarm()
    {
        Edit();
        var rendered = await _voiceEncoder.GetMultipleSequencesAudio([_sequence], _voiceCache);
        return rendered.Audio.GetLength();
    }
}

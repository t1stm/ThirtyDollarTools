using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Voices;

namespace ThirtyDollarConverter.Benchmarks;

/// <summary>
///     The editor loop on a real project (amalgamam.tdwproj): apply one edit, rebuild the merged
///     sequence, re-render. Nearly every note in it carries cut automation, the hardest case for
///     an incremental render.
/// </summary>
[MemoryDiagnoser(false)]
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 3, invocationCount: 1)]
public class EditorWorkflowBenchmarks
{
    private EditorEdits _edits = null!;
    private PcmEncoder _encoder = null!;
    private RenderedSequence _rendered = null!;
    private int _step;
    private Dictionary<(string, double), ProcessedEvent> _voiceCache = null!;
    private PcmEncoder _voiceEncoder = null!;

    [Params(EditKind.AutomatedNoteValue, EditKind.AutomatedNoteVolume, EditKind.PlainNoteValue,
        EditKind.MoveNote, EditKind.AddRemoveNote, EditKind.RetuneInstrument)]
    public EditKind Edit { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _encoder = Workbench.Encoder();
        _edits = new EditorEdits(Workbench.LoadProject());
        _rendered = _encoder.GetSequenceAudio(_edits.Project.ToSequence()).GetAwaiter().GetResult();

        _voiceEncoder = Workbench.Encoder(RenderMethod.Voice);
        using var voices = _voiceEncoder.BuildVoices([_edits.Project.ToSequence()]).GetAwaiter().GetResult();
        _voiceCache = voices.ProcessedEvents;
    }

    [Benchmark(Baseline = true)]
    public async Task<int> FullRender()
    {
        _edits.Apply(Edit, ++_step);
        var rendered = await _encoder.GetSequenceAudio(_edits.Project.ToSequence());
        return rendered.Audio.GetLength();
    }

    /// <summary>
    ///     Re-render from scratch but keep the resampled samples - what the incremental renderer
    ///     falls back to internally, and the honest "just re-render it" baseline.
    /// </summary>
    [Benchmark]
    public async Task<int> FullRenderWarm()
    {
        _edits.Apply(Edit, ++_step);
        var rendered = await _encoder.GetMultipleSequencesAudio([_edits.Project.ToSequence()],
            _rendered.ProcessedEvents);
        return rendered.Audio.GetLength();
    }

    [Benchmark]
    public async Task<int> Incremental()
    {
        _edits.Apply(Edit, ++_step);
        _rendered = await _encoder.ComputeIncrementalAudio(_rendered, [_edits.Project.ToSequence()]);
        return _rendered.Audio.GetLength();
    }

    /// <summary>
    ///     What an edit costs the streaming player: place, look the resampled sounds up and
    ///     rebuild the voice list. Nothing is mixed ahead - playback mixes as it goes.
    /// </summary>
    [Benchmark]
    public async Task<int> VoiceRebuild()
    {
        _edits.Apply(Edit, ++_step);
        using var render = await _voiceEncoder.BuildVoices([_edits.Project.ToSequence()], _voiceCache);
        return render.Voices.Count;
    }

    /// <summary>A whole render through the voice mixer, resampled sounds kept - what an export costs.</summary>
    [Benchmark]
    public async Task<int> VoiceRenderWarm()
    {
        _edits.Apply(Edit, ++_step);
        var rendered = await _voiceEncoder.GetMultipleSequencesAudio([_edits.Project.ToSequence()], _voiceCache);
        return rendered.Audio.GetLength();
    }
}

using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Benchmarks;

/// <summary>
///     Correctness check: for every scenario, apply the same edits to two copies and render one
///     incrementally and the other from scratch, then report how far the incremental result drifts
///     from the full render it is supposed to reproduce. Expected drift is zero - the incremental
///     renderer replays the same placements over the same chunk grid, so it produces the same
///     samples bit for bit.
/// </summary>
public static class Verify
{
    public static async Task Run()
    {
        Console.WriteLine($"{"scenario",-34} {"edits",5} {"Δ",12} {"incremental",12}  lengths");

        foreach (var cover in Covers.All) await VerifyCover(cover);
        foreach (var kind in Enum.GetValues<EditKind>()) await VerifyEditorEdit(kind);

        Console.WriteLine($"\n{"voice vs legacy",-34} {"Δ",12} {"Δ dB",8} {">1e-3",7}  lengths");
        foreach (var cover in Covers.All) await VerifyVoice(cover.Name, () => Workbench.LoadSequence(cover.Path));
        foreach (var (name, path) in VoiceProjects)
            await VerifyVoice(name, () => path.EndsWith(".tdwproj")
                ? ProjectFile.Load(File.ReadAllText(Path.Combine(Workbench.SequenceRoot, path))).ToSequence()
                : Workbench.LoadSequence(path));
    }

    /// <summary>The projects the voice mixer was measured on, under <see cref="Workbench.SequenceRoot" />.</summary>
    private static readonly (string Name, string Path)[] VoiceProjects =
    [
        ("flower-castle", "flower castle/Flower Castle TDW Cover - Final.tdw"),
        ("amalgam", "amalgam/Amalgam.tdw"),
        ("wonder", "wonder/wonder.tdwproj"),
        ("xnor", "XNOR XNOR XNOR/XNOR XNOR XNOR - MOON.tdw")
    ];

    /// <summary>
    ///     A whole song through the voice mixer against the legacy track renderer: the peak
    ///     difference, that peak against the song's own peak, and how many samples are off by
    ///     more than 1e-3. Expected: float-summation noise only.
    /// </summary>
    private static async Task VerifyVoice(string name, Func<Sequence> load)
    {
        Sequence sequence;
        try
        {
            sequence = load();
        }
        catch (IOException e)
        {
            Console.WriteLine($"{$"voice/{name}",-34} skipped: {e.Message}");
            return;
        }

        var legacy = (await Workbench.Encoder().GetSequenceAudio(sequence)).Audio;
        var voice = (await Workbench.Encoder(RenderMethod.Voice).GetSequenceAudio(sequence)).Audio;
        var (legacy_length, voice_length, delta) = Workbench.Compare(legacy, voice);

        var peak = 0f;
        var off = 0;
        for (var channel = 0; channel < legacy.ChannelCount; channel++)
        for (var i = 0; i < Math.Min(legacy_length, voice_length); i++)
        {
            peak = Math.Max(peak, Math.Abs(legacy.Samples[channel][i]));
            if (Math.Abs(legacy.Samples[channel][i] - voice.Samples[channel][i]) > 1e-3f) off++;
        }

        var lengths = legacy_length == voice_length
            ? $"{legacy_length} (equal)"
            : $"legacy {legacy_length}, voice {voice_length}";
        Console.WriteLine($"{$"voice/{name}",-34} {delta,12:E3} {20 * Math.Log10(delta / peak),8:F1} {off,7}  {lengths}");
    }

    private static async Task VerifyCover(Cover cover)
    {
        const int edits = 4;
        var encoder = Workbench.Encoder();

        var incremental_sequence = Workbench.LoadSequence(cover.Path);
        var full_sequence = Workbench.LoadSequence(cover.Path);

        var incremental = await encoder.GetSequenceAudio(incremental_sequence);
        var incremental_count = 0;

        for (var step = 1; step <= edits; step++)
        {
            Covers.RetuneMiddleEvent(incremental_sequence, step);
            Covers.RetuneMiddleEvent(full_sequence, step);

            var before = incremental;
            incremental = await encoder.ComputeIncrementalAudio(incremental, [incremental_sequence]);
            if (ReferenceEquals(before, incremental)) incremental_count++;
        }

        var full = await encoder.GetSequenceAudio(full_sequence);
        Report($"cover/{cover.Name}", edits, incremental_count, full, incremental);
    }

    private static async Task VerifyEditorEdit(EditKind kind)
    {
        const int edits = 4;
        var encoder = Workbench.Encoder();

        var incremental_edits = new EditorEdits(Workbench.LoadProject());
        var full_edits = new EditorEdits(Workbench.LoadProject());

        var incremental = await encoder.GetSequenceAudio(incremental_edits.Project.ToSequence());
        var incremental_count = 0;

        for (var step = 1; step <= edits; step++)
        {
            incremental_edits.Apply(kind, step);
            full_edits.Apply(kind, step);

            var before = incremental;
            incremental = await encoder.ComputeIncrementalAudio(incremental,
                [incremental_edits.Project.ToSequence()]);
            if (ReferenceEquals(before, incremental)) incremental_count++;
        }

        var full = await encoder.GetSequenceAudio(full_edits.Project.ToSequence());
        Report($"editor/{kind}", edits, incremental_count, full, incremental);
    }

    /// <summary>
    ///     <see cref="PcmEncoder.ComputeIncrementalAudio" /> mutates and hands back the same
    ///     <see cref="RenderedSequence" /> when it takes the incremental path, and returns a fresh one
    ///     from <c>GetMultipleSequencesAudio</c> when it bails out to a full render - so identity is
    ///     how many of the edits it actually managed incrementally.
    /// </summary>
    private static void Report(string scenario, int edits, int incrementalCount, RenderedSequence full,
        RenderedSequence incremental)
    {
        var (full_length, incremental_length, delta) = Workbench.Compare(full.Audio, incremental.Audio);

        var lengths = full_length == incremental_length
            ? $"{full_length} (equal)"
            : $"full {full_length}, incremental {incremental_length}";

        Console.WriteLine($"{scenario,-34} {edits,5} {delta,12:E3} " +
                          $"{$"{incrementalCount}/{edits}",12}  {lengths}");
    }
}
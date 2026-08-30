using ThirtyDollarConverter.Objects;

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
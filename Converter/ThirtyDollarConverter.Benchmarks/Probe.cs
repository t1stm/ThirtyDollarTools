using System.Diagnostics;

namespace ThirtyDollarConverter.Benchmarks;

/// <summary>
///     Prints the shape and cold-render cost of every scenario - used to size the
///     benchmark matrix, not part of it.
/// </summary>
public static class Probe
{
    public static async Task Run()
    {
        var encoder = Workbench.Encoder();
        Console.WriteLine($"samples loaded: {Workbench.Samples().SampleList.Count}");

        foreach (var cover in Covers.All)
        {
            var sequence = Workbench.LoadSequence(cover.Path);
            var start = Stopwatch.GetTimestamp();
            var rendered = await encoder.GetSequenceAudio(sequence);
            var elapsed = Stopwatch.GetElapsedTime(start);

            Console.WriteLine($"{cover.Name,-28} events: {sequence.Events.Length,6}  " +
                              $"placements: {rendered.Placement.Length,7}  " +
                              $"samples: {rendered.Audio.GetLength(),9} " +
                              $"({rendered.Audio.GetLength() / 48000.0,6:F1}s)  " +
                              $"tracks: {rendered.Mixer!.GetTracks().Length,3}  " +
                              $"full render: {elapsed.TotalMilliseconds,8:F0} ms");
        }

        var project = Workbench.LoadProject();
        Console.WriteLine($"\n{Path.GetFileName(Workbench.EditorProjectPath)}: " +
                          $"{project.Tracks.Count} tracks, " +
                          $"{project.Tracks.SelectMany(t => t.Segments).SelectMany(s => s.Notes).Count()} notes, " +
                          $"{project.Instruments.Count} instruments, {project.Placements.Count} placements");

        foreach (var track in project.Tracks)
            Console.WriteLine($"  track {track.Id,2} \"{track.Name}\": " +
                              $"{track.Segments.Count} segments, " +
                              $"{track.Segments.Sum(s => s.Notes.Count)} notes, " +
                              $"{track.Segments.SelectMany(s => s.Notes).Count(n => n.Automation != null)} automated");

        var merged = project.ToSequence();
        var project_start = Stopwatch.GetTimestamp();
        var project_rendered = await encoder.GetSequenceAudio(merged);
        var project_elapsed = Stopwatch.GetElapsedTime(project_start);
        Console.WriteLine($"  merged: events: {merged.Events.Length}, " +
                          $"placements: {project_rendered.Placement.Length}, " +
                          $"samples: {project_rendered.Audio.GetLength()} " +
                          $"({project_rendered.Audio.GetLength() / 48000.0:F1}s), " +
                          $"tracks: {project_rendered.Mixer!.GetTracks().Length}, " +
                          $"full render: {project_elapsed.TotalMilliseconds:F0} ms");
    }
}
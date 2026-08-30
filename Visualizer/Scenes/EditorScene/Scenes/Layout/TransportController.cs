using Sundex.Components.Bars;
using Sundex.Components.Labels;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     Drives the transport block docked in the track column. The tree lives in
///     Scenes/Layout/Transport Section/TransportSection.snx.xml and its handles are resolved
///     by that document's logic; this type only refreshes it per frame.
/// </summary>
internal sealed class TransportController(
    EditorPlayback playback,
    ProgressBar progress,
    Label elapsed,
    Label total,
    Button play)
{
    /// <summary>Per-frame: play/pause label always, progress/elapsed/total only while a session exists.</summary>
    public void Refresh()
    {
        if (playback.HasSession)
        {
            var elapsedMs = playback.ElapsedMs;
            var totalMs = playback.TotalMs;
            progress.Progress = totalMs > 0 ? (float)elapsedMs / totalMs : 0;
            elapsed.SetTextContents(TimeString(elapsedMs));
            total.SetTextContents(TimeString(totalMs));
        }

        play.Label.SetTextContents(playback.IsPlaying ? "Pause" : "Play");
    }

    private static string TimeString(long ms)
    {
        return $"{ms / 60000}:{ms / 1000 % 60:00}";
    }
}

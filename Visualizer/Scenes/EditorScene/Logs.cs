using System.Diagnostics;
using EditorScene.State;
using Microsoft.Extensions.Logging;
using ThirtyDollarConverter.Editor;

namespace EditorScene;

/// <summary>
///     The log lines the editor writes through Microsoft.Extensions.Logging, source-generated.
///     The Information ones mark a step landing - a dialog mounting, a track opening, an event
///     going in, a render finishing - so a headless run can wait on the line instead of
///     sleeping (the visualizer-headless skill's <c>viz.sh wait</c>). They are Debug builds
///     only: Conditional drops each call, arguments included, from a Release build. The
///     Error ones always log.
/// </summary>
internal static partial class Logs
{
    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Dialog opened: {Dialog}")]
    public static partial void DialogOpened(this ILogger logger, string dialog);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Dialog closed: {Dialog}")]
    public static partial void DialogClosed(this ILogger logger, string dialog);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Track added: {Track} ({Kind})")]
    public static partial void TrackAdded(this ILogger logger, string track, TrackKind kind);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Instrument added: {Instrument}")]
    public static partial void InstrumentAdded(this ILogger logger, string instrument);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Track converted: {Track} ({Kind})")]
    public static partial void TrackConverted(this ILogger logger, string track, TrackKind kind);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Track removed: {Track}")]
    public static partial void TrackRemoved(this ILogger logger, string track);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Track selected: {Track} ({Count} selected)")]
    public static partial void TrackSelected(this ILogger logger, string? track, int count);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Track opened: {Track} ({Kind})")]
    public static partial void TrackOpened(this ILogger logger, string track, TrackKind kind);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Track closed: {Track}")]
    public static partial void TrackClosed(this ILogger logger, string track);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Note added at step {Step}: {Instrument} {Value} (cut: {IsCut})")]
    public static partial void NoteAdded(this ILogger logger, int step, string instrument, double value, bool isCut);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Item added at {Index}: {Item} ({Count} items)")]
    public static partial void ItemAdded(this ILogger logger, int index, string? item, int count);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Clip placed: {Track} on channel {Channel} at {Start} quarters")]
    public static partial void ClipPlaced(this ILogger logger, string track, int channel, double start);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Pasted {Count} {What}")]
    public static partial void Pasted(this ILogger logger, int count, string what);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Deleted {Count} {What}")]
    public static partial void Deleted(this ILogger logger, int count, string what);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Undo")]
    public static partial void Undone(this ILogger logger);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Redo")]
    public static partial void Redone(this ILogger logger);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Tool: {Tool}")]
    public static partial void ToolChanged(this ILogger logger, EditorTool tool);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "New project")]
    public static partial void ProjectCreated(this ILogger logger);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Project loaded: {Path} ({Count} tracks)")]
    public static partial void ProjectLoaded(this ILogger logger, string path, int count);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Project saved: {Path}")]
    public static partial void ProjectSaved(this ILogger logger, string path);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Imported {Path} as {Mode}")]
    public static partial void Imported(this ILogger logger, string path, ImportMode mode);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Exported {Path}")]
    public static partial void Exported(this ILogger logger, string path);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Information, "Render finished in {Elapsed} ms")]
    public static partial void RenderFinished(this ILogger logger, long elapsed);

    [LoggerMessage(LogLevel.Error, "Failed to load project \"{Path}\"")]
    public static partial void LoadFailed(this ILogger logger, Exception exception, string path);

    [LoggerMessage(LogLevel.Error, "Failed to save project \"{Path}\"")]
    public static partial void SaveFailed(this ILogger logger, Exception exception, string path);

    [LoggerMessage(LogLevel.Error, "Failed to import \"{Path}\"")]
    public static partial void ImportFailed(this ILogger logger, Exception exception, string path);

    [LoggerMessage(LogLevel.Error, "Failed to export \"{Path}\"")]
    public static partial void ExportFailed(this ILogger logger, Exception exception, string path);

    [LoggerMessage(LogLevel.Error, "Failed to write backup")]
    public static partial void BackupFailed(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Preview render failed")]
    public static partial void PreviewRenderFailed(this ILogger logger, Exception? exception);

    [LoggerMessage(LogLevel.Error, "Render failed")]
    public static partial void RenderFailed(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Couldn't read wave file \"{Path}\"")]
    public static partial void WaveReadFailed(this ILogger logger, Exception exception, string path);
}

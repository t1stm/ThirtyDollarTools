using System.Diagnostics;
using Serilog;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using EditorScene.Scenes.Dialogs;

namespace EditorScene;

/// <summary>
///     Project file lifecycle for <see cref="Scenes.EditorInterface" />: save/load, TDW
///     import/export, and timestamped backups on a dirty timer. Failures are logged and
///     reported through the dialog host rather than thrown.
/// </summary>
public sealed class ProjectIO(EditorState state, DialogHost dialogHost, ILogger logger)
{
    private const long BackupIntervalMs = 5 * 60_000;
    private const int MaxBackups = 20;
    private static readonly string BackupDirectory = Path.Combine(AppContext.BaseDirectory, "Editor Backups");

    private readonly Stopwatch _sinceBackup = Stopwatch.StartNew();

    /// <summary>Saving clears Dirty without firing OnProjectChanged, so the owner hooks its title refresh here.</summary>
    public Action? OnSaved { get; set; }

    public void Load(string path)
    {
        try
        {
            state.LoadProjectFromFile(path);
        }
        catch (Exception e)
        {
            logger.Error("[Editor] Failed to load project \"{Path}\": {Exception}", path, e);
            dialogHost.Alert($"Couldn't open \"{Path.GetFileName(path)}\":\n{e.Message}");
        }
    }

    /// <summary>Saves to the known path, or asks for one; runs the continuation only on success.</summary>
    public void Save(Action? andThen = null)
    {
        if (state.ProjectPath is { } path)
        {
            if (Write(path)) andThen?.Invoke();
            return;
        }

        dialogHost.ShowFileDialog($"{state.Project.Info.Name}.tdwproj", ".tdwproj", picked =>
        {
            if (Write(picked)) andThen?.Invoke();
        }, "Save");
    }

    /// <summary>
    ///     Imports a TDW sequence file as a track or as a whole project (see
    ///     <see cref="ImportMode" />). All-or-nothing: a parse or import failure leaves the
    ///     project untouched. Non-fatal issues (ignored events, quantized notes, unknown
    ///     sounds) surface as one summary alert.
    /// </summary>
    public void ImportTdw(string path, ImportMode mode, IReadOnlyDictionary<string, Sound>? soundMap)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        try
        {
            var sequence = Sequence.FromString(File.ReadAllText(path));
            var result = mode switch
            {
                ImportMode.Track => state.ImportSequenceAsTrack(sequence, name, soundMap),
                ImportMode.Faithful => state.ImportSequenceAsTrack(sequence, name, soundMap, TrackKind.Faithful),
                _ => state.ReplaceWithImportedProject(sequence, name, soundMap)
            };

            if (!result.Warnings.IsEmpty)
                dialogHost.Alert($"Imported with warnings:\n\n{Summarize(result.Warnings)}");
        }
        catch (Exception e)
        {
            logger.Error("[Editor] Failed to import \"{Path}\": {Exception}", path, e);
            dialogHost.Alert($"Couldn't import \"{Path.GetFileName(path)}\":\n\n{e.Message}");
        }
    }

    private static string Summarize(ImportWarnings warnings)
    {
        var parts = new List<string>();
        foreach (var (name, count) in warnings.IgnoredEvents)
            parts.Add($"ignored {name} ×{count}");
        if (warnings.QuantizedNotes > 0)
            parts.Add($"{warnings.QuantizedNotes} note{(warnings.QuantizedNotes == 1 ? "" : "s")} quantized");
        foreach (var sound in warnings.UnknownSounds)
            parts.Add($"unknown sound: {sound}");
        // One warning per line: the alert's label doesn't wrap, so a joined line runs
        // straight out of the modal.
        return string.Join("\n", parts);
    }

    public void ExportTdw(string path, SequenceStyle style)
    {
        try
        {
            File.WriteAllText(path, SequenceText.Serialize(state.Project.ToSequence(style)));
        }
        catch (Exception e)
        {
            logger.Error("[Editor] Failed to export \"{Path}\": {Exception}", path, e);
            dialogHost.Alert($"Couldn't export \"{Path.GetFileName(path)}\":\n{e.Message}");
        }
    }

    /// <summary>Call once per frame; writes a timestamped backup every <see cref="BackupIntervalMs" /> while dirty.</summary>
    public void TickBackup()
    {
        if (!state.Dirty || _sinceBackup.ElapsedMilliseconds < BackupIntervalMs) return;
        WriteBackup();
        _sinceBackup.Restart();
    }

    private bool Write(string path)
    {
        try
        {
            state.SaveProjectToFile(path);
            OnSaved?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            logger.Error("[Editor] Failed to save project \"{Path}\": {Exception}", path, e);
            dialogHost.Alert($"Couldn't save \"{Path.GetFileName(path)}\":\n{e.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Writes a timestamped snapshot next to the executable. Leaves ProjectPath and Dirty
    ///     alone, so it stays invisible to the normal save flow; only <see cref="TickBackup" />
    ///     calls it. Failures are logged, not shown to the user.
    /// </summary>
    private void WriteBackup()
    {
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            var name = string.Concat(state.Project.Info.Name.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{name}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.tdwproj";
            File.WriteAllText(Path.Combine(BackupDirectory, fileName), ProjectFile.Save(state.Project));
            PruneBackups();
        }
        catch (Exception e)
        {
            logger.Error("[Editor] Failed to write backup: {Exception}", e);
        }
    }

    /// <summary>
    ///     Deletes all but the newest <see cref="MaxBackups" /> files, so a long session's
    ///     repeated backups stay bounded.
    /// </summary>
    private void PruneBackups()
    {
        var files = new DirectoryInfo(BackupDirectory).GetFiles("*.tdwproj");
        foreach (var stale in files.OrderByDescending(f => f.LastWriteTimeUtc).Skip(MaxBackups))
            stale.Delete();
    }
}
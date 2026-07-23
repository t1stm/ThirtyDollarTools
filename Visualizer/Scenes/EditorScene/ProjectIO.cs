using System.Diagnostics;
using EditorScene.Scenes.Components;
using Serilog;
using ThirtyDollarConverter.Editor;

namespace EditorScene;

/// <summary>
///     Project file lifecycle: save/load, timestamped backups on a dirty timer, TDW
///     export. Lifted out of <see cref="Scenes.EditorInterface" />.
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
        });
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

    /// <summary>Timestamped snapshot next to the executable — doesn't touch ProjectPath/Dirty,
    /// so it's invisible to the normal save flow (only <see cref="TickBackup" /> drives it).
    /// Logged, not surfaced — a failed background backup isn't the data-loss path a failed
    /// explicit save is, and popping a dialog on a timer would be its own annoyance.</summary>
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

    /// <summary>Keeps only the newest <see cref="MaxBackups" /> files — a long session
    /// backs up every 5 dirty minutes and never stopped growing otherwise.</summary>
    private void PruneBackups()
    {
        var files = new DirectoryInfo(BackupDirectory).GetFiles("*.tdwproj");
        foreach (var stale in files.OrderByDescending(f => f.LastWriteTimeUtc).Skip(MaxBackups))
            stale.Delete();
    }
}

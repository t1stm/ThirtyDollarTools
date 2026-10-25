using EditorScene.State;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
            logger.ProjectLoaded(path, state.Project.Tracks.Count);
        }
        catch (Exception e)
        {
            logger.LoadFailed(e, path);
            dialogHost.Alert($"Couldn't open {Path.GetFileName(path)}", e);
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

        SaveAs(andThen);
    }

    /// <summary>
    ///     Always asks where, even when the project already has a file; it lives at the picked one
    ///     afterwards. Opens beside the current file, or in the home directory for a project that
    ///     has never been saved.
    /// </summary>
    public void SaveAs(Action? andThen = null)
    {
        var name = state.ProjectPath is { } path ? Path.GetFileName(path) : $"{state.Project.Info.Name}.tdwproj";
        dialogHost.ShowFileDialog("Save project as", name, ".tdwproj", picked =>
        {
            if (Write(picked)) andThen?.Invoke();
        }, "Save", Path.GetDirectoryName(state.ProjectPath));
    }

    /// <summary>
    ///     Reads a TDW sequence file and dry-runs every <see cref="ImportMode" /> on it, for the
    ///     import dialog to show. A file that can't be read or parsed is reported here and gives
    ///     null, so no dialog opens.
    /// </summary>
    public (Sequence Sequence, ImportPreview Preview)? ReadTdw(string path,
        IReadOnlyDictionary<string, Sound>? soundMap)
    {
        try
        {
            var sequence = Sequence.FromString(File.ReadAllText(path));
            return (sequence, SequenceImporter.Preview(sequence, soundMap));
        }
        catch (Exception e)
        {
            ImportFailed(e, path);
            return null;
        }
    }

    /// <summary>
    ///     Imports a sequence read by <see cref="ReadTdw" /> as a track or as a whole project (see
    ///     <see cref="ImportMode" />). All-or-nothing: an import failure leaves the project
    ///     untouched and is reported here. Non-fatal issues (ignored events, quantized notes,
    ///     unknown sounds) are returned for the owner to show; null means the import failed.
    /// </summary>
    /// <param name="name">The new track's or project's name.</param>
    public ImportWarnings? ImportTdw(string path, Sequence sequence, ImportMode mode, string name,
        IReadOnlyDictionary<string, Sound>? soundMap)
    {
        try
        {
            var result = mode switch
            {
                ImportMode.Track => state.ImportSequenceAsTrack(sequence, name, soundMap),
                ImportMode.Faithful => state.ImportSequenceAsTrack(sequence, name, soundMap, TrackKind.Faithful),
                _ => state.ReplaceWithImportedProject(sequence, name, soundMap)
            };

            logger.Imported(path, mode);
            return result.Warnings;
        }
        catch (Exception e)
        {
            ImportFailed(e, path);
            return null;
        }
    }

    private void ImportFailed(Exception e, string path)
    {
        logger.ImportFailed(e, path);
        dialogHost.Alert($"Couldn't import {Path.GetFileName(path)}", e);
    }

    public void ExportTdw(string path, SequenceStyle style)
    {
        try
        {
            File.WriteAllText(path, SequenceText.Serialize(state.Project.ToSequence(style)));
            logger.Exported(path);
        }
        catch (Exception e)
        {
            logger.ExportFailed(e, path);
            dialogHost.Alert($"Couldn't export {Path.GetFileName(path)}", e, writing: true);
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
            logger.ProjectSaved(path);
            return true;
        }
        catch (Exception e)
        {
            logger.SaveFailed(e, path);
            dialogHost.Alert($"Couldn't save {Path.GetFileName(path)}", e, writing: true);
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
            logger.BackupFailed(e);
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
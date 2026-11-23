using System.Runtime.Versioning;
using System.Text.Json;
using Shared;

namespace ThirtyDollarVisualizer.WASM;

/// <summary>
///     The folder the user picked. Its listing sits in the virtual filesystem under
///     <see cref="Root" /> as empty entries, so the pickers can browse it without the browser
///     reading every file; a file's contents come from the folder when it is opened.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class BrowserWorkspace : Workspace
{
    private const string MountPoint = "/workspace";

    public override string Root => MountPoint;

    /// <param name="entries">The folder's files and folders, as workspace.js lists them.</param>
    public static BrowserWorkspace Mount(string entries)
    {
        Directory.CreateDirectory(MountPoint);
        foreach (var entry in JsonSerializer.Deserialize<Entry[]>(entries) ?? [])
        {
            var path = Path.Combine(MountPoint, Relative(entry.Path));
            if (entry.IsDirectory) Directory.CreateDirectory(path);
            else File.Create(path).Dispose();
        }

        return new BrowserWorkspace();
    }

    /// <summary>Relative to the folder, so a project opens wherever the folder is picked from.</summary>
    public override string ProjectPath(string path) => Relative(path);

    public override async Task<Stream> Open(string path)
    {
        using var file = await Program.ReadWorkspaceFile(Relative(path));
        return new MemoryStream(file.GetPropertyAsByteArray("bytes") ??
                                throw new IOException("The browser returned no file contents."));
    }

    public override async Task Write(string path, Action<Stream> write)
    {
        var relative = Relative(path);
        using var buffer = new MemoryStream();
        write(buffer);
        await Program.WriteWorkspaceFile(relative, buffer.ToArray());
        // Its empty entry, made or dated now: the pickers list a new file, and a loaded sequence reloads.
        File.WriteAllBytes(Path.Combine(MountPoint, relative), []);
    }

    /// <summary>The path inside the folder, with '/' between parts. A relative path counts from the folder.</summary>
    private static string Relative(string path)
    {
        var relative = Path.GetRelativePath(MountPoint, Path.GetFullPath(path, MountPoint));
        if (relative is "." or ".." || Path.IsPathRooted(relative) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException("The path is outside the selected folder.");
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private sealed record Entry(string Path, bool IsDirectory);
}

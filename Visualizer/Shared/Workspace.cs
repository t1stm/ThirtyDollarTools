using System.Text;

namespace Shared;

/// <summary>
///     Where the tools open and save the user's files. This one is the local disk; the browser
///     build swaps in the folder the user picked. The disk's calls complete before they return,
///     so code that awaits them runs straight through on the desktop.
/// </summary>
public class Workspace
{
    /// <summary>A folder the file pickers can't leave, or null for the whole disk.</summary>
    public virtual string? Root => null;

    /// <summary>How a project refers to the file at <paramref name="path" />.</summary>
    public virtual string ProjectPath(string path) => path;

    public virtual Task<Stream> Open(string path) => Task.FromResult<Stream>(File.OpenRead(path));

    /// <summary>
    ///     Writes the file through <paramref name="write" />. It lands beside the target and moves
    ///     into place, so a write that fails part way (a full disk, say) leaves the old file as it was.
    /// </summary>
    public virtual Task Write(string path, Action<Stream> write)
    {
        // ponytail: the move replaces a symlink and the old file's permissions instead of keeping
        // them; resolve the link and copy the mode first if that ever matters.
        var temporary = path + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            {
                write(stream);
                // On disk before the rename, or a power cut could leave the new name with no data.
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task<string> ReadText(string path)
    {
        using var stream = await Open(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public Task WriteText(string path, string text) => Write(path, stream => stream.Write(Encoding.UTF8.GetBytes(text)));
}

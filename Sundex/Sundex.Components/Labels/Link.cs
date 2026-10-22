using System.ComponentModel;
using System.Diagnostics;
using Sundex.Components.Abstractions;

namespace Sundex.Components.Labels;

/// <summary>
///     A label that opens <see cref="Url" /> with the OS handler when clicked: http(s) in the
///     browser, file:// (or a plain path) in the file manager or its default app, any other
///     registered scheme (mailto:, steam://, ...) in whatever claims it.
/// </summary>
public class Link : Label
{
    public Link(UIContext context, ReadOnlySpan<char> text, string url, string? font = null) : base(context, text,
        font)
    {
        Url = url;
        UpdateCursorOnHover = true;
        OnClick = _ => Open();
    }

    public string Url { get; set; }

    public override string Tag => "link";

    /// <summary>
    ///     Hands <see cref="Url" /> to the shell: ShellExecute on Windows, <c>open</c> on macOS,
    ///     <c>xdg-open</c> on Linux. False when the OS had nothing to open it with.
    /// </summary>
    public bool Open()
    {
        if (string.IsNullOrWhiteSpace(Url)) return false;

        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Win32Exception)
        {
            // ponytail: a dead link shouldn't take the app down; nothing in Sundex logs yet.
            return false;
        }
    }
}

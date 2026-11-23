using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace ThirtyDollarVisualizer.WASM;

/// <summary>What main.js calls and what it provides. The page's state lives in <see cref="BrowserHost" />.</summary>
[SupportedOSPlatform("browser")]
internal static partial class Program
{
    private static BrowserHost _host = null!;

    private static void Main() { }

    /// <param name="workspaceEntries">The picked folder's files and folders, as workspace.js lists them.</param>
    /// <param name="verify">Whether the page checks for WebGL errors and reports its state, for the browser check.</param>
    [JSExport]
    internal static void Start(int width, int height, int pixelsWide, int pixelsHigh, string assetBaseUri,
        string workspaceEntries, bool verify) =>
        _host = new BrowserHost((width, height), (pixelsWide, pixelsHigh), new Uri(assetBaseUri),
            BrowserWorkspace.Mount(workspaceEntries), verify);

    [JSExport]
    internal static void Resize(int width, int height, int pixelsWide, int pixelsHigh) =>
        _host.Resize((width, height), (pixelsWide, pixelsHigh));

    /// <param name="buttons">Held buttons as bits indexed by MouseButton; DOM button bits match it for the first three.</param>
    [JSExport]
    internal static void Tick(double delta, float x, float y, float scrollX, float scrollY,
        int buttons, int previousButtons, int pressed, int released, bool inside) =>
        _host.Tick(delta, (x, y), (scrollX, scrollY), (uint)buttons, (uint)previousButtons, (uint)pressed,
            (uint)released, inside);

    /// <param name="code">The event's <c>code</c>.</param>
    /// <param name="text">What the key types, or empty.</param>
    /// <returns>Whether the app took the key, so the page stops the browser's own handling.</returns>
    [JSExport]
    internal static bool KeyDown(string code, string text, bool repeat, bool shift, bool control, bool alt,
        bool meta, bool capsLock, bool numLock) =>
        _host.KeyDown(code, text, repeat, shift, control, alt, meta, capsLock, numLock);

    [JSExport]
    internal static void KeyUp(string code) => _host.KeyUp(code);

    [JSExport]
    internal static void ClearKeys() => _host.ClearKeys();

    [JSExport]
    internal static void PumpAudio() => _host.PumpAudio();

    [JSExport]
    internal static string ActiveScene() => _host.ActiveScene;

    [JSExport]
    internal static double PlaybackMilliseconds() => _host.PlaybackMilliseconds;

    [JSImport("setTitle", "visualizer")]
    internal static partial void SetTitle(string title);

    [JSImport("setCursor", "visualizer")]
    internal static partial void SetCursor(string cursor);

    [JSImport("writeClipboard", "visualizer")]
    internal static partial void WriteClipboard(string text);

    [JSImport("readSettings", "visualizer")]
    internal static partial string? ReadSettings();

    [JSImport("writeSettings", "visualizer")]
    internal static partial void WriteSettings(string text);

    /// <returns>An object whose <c>bytes</c> holds the file: a promise can't carry a byte array itself.</returns>
    [JSImport("readWorkspaceFile", "visualizer")]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    internal static partial Task<JSObject> ReadWorkspaceFile(string path);

    [JSImport("writeWorkspaceFile", "visualizer")]
    internal static partial Task WriteWorkspaceFile(string path, byte[] bytes);
}

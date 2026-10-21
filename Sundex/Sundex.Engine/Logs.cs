using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Sundex.Engine;

/// <summary>
///     The engine's log lines written through Microsoft.Extensions.Logging, source-generated.
///     The input ones are a once-a-frame trace, Debug builds only (see Game.LogInput): each
///     line means a frame has already handled that input, so the visualizer-headless skill's
///     viz.sh waits on them instead of sleeping between steps.
/// </summary>
internal static partial class Logs
{
    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Debug, "Pointer at {X}, {Y}")]
    public static partial void PointerMoved(this ILogger logger, int x, int y);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Debug, "Pointer pressed: {Button} at {X}, {Y}")]
    public static partial void PointerPressed(this ILogger logger, string button, int x, int y);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Debug, "Pointer released: {Button} at {X}, {Y}")]
    public static partial void PointerReleased(this ILogger logger, string button, int x, int y);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Debug, "Scrolled {X}, {Y}")]
    public static partial void Scrolled(this ILogger logger, float x, float y);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Debug, "Key pressed: {Key}")]
    public static partial void KeyPressed(this ILogger logger, Keys key);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Debug, "Key released: {Key}")]
    public static partial void KeyReleased(this ILogger logger, Keys key);

    [Conditional("DEBUG")]
    [LoggerMessage(LogLevel.Debug, "Text input: {Text}")]
    public static partial void TextEntered(this ILogger logger, string text);
}

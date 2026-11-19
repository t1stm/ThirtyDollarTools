namespace Sundex.Engine;

/// <summary>
///     The system clipboard. <see cref="Game" /> connects it to the window when it loads; until then,
///     and in tests, it is a string held in memory.
/// </summary>
public static class Clipboard
{
    private static string _held = "";

    public static Func<string> Read { get; set; } = () => _held;
    public static Action<string> Write { get; set; } = text => _held = text;
}

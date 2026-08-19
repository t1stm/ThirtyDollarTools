using System.Text;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace VisualizerScene.Settings;

/// <summary>
///     One shortcut: a key plus the exact set of modifiers that must be held with it.
///     Written as "Ctrl+Shift+Z"; read back with the modifiers in any order.
/// </summary>
public readonly record struct Keybind(Keys Key, KeyModifiers Modifiers)
{
    /// <summary>
    ///     The only modifiers a binding can name. Everything else - CapsLock and NumLock,
    ///     which GLFW reports in the same bitfield - is masked out before comparing, or
    ///     every shortcut would stop working for anyone with Caps on.
    /// </summary>
    private const KeyModifiers Relevant =
        KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Super;

    /// <summary>What <see cref="KeyModifiers.Super" /> is called on this platform.</summary>
    private static readonly string SuperName = OperatingSystem.IsMacOS() ? "Cmd" : "Super";

    /// <summary>
    ///     Parses "Ctrl+Shift+Z", "Cmd+Z", "Space". Modifier order doesn't matter and
    ///     Super/Cmd/Win/Meta are all the same modifier, so a settings file hand-edited on
    ///     one platform still reads on another.
    /// </summary>
    public static bool TryParse(string? text, out Keybind bind)
    {
        bind = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        var modifiers = (KeyModifiers)0;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var modifier = ParseModifier(parts[i]);
            if (modifier == null) return false;
            modifiers |= modifier.Value;
        }

        // TryParse also accepts the raw numbers, so IsDefined is what keeps "Undo:7" out.
        if (!Enum.TryParse<Keys>(parts[^1], true, out var key) || !Enum.IsDefined(key)) return false;

        bind = new Keybind(key, modifiers);
        return true;
    }

    /// <summary>The binding a key event would be, with the lock keys dropped. For the capture UI.</summary>
    public static Keybind From(KeyboardKeyEventArgs e)
    {
        return new Keybind(e.Key, e.Modifiers & Relevant);
    }

    private static KeyModifiers? ParseModifier(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "ctrl" or "control" => KeyModifiers.Control,
            "shift" => KeyModifiers.Shift,
            "alt" or "option" => KeyModifiers.Alt,
            "super" or "cmd" or "command" or "win" or "meta" => KeyModifiers.Super,
            _ => null
        };
    }

    /// <summary>Canonical spelling: Ctrl, Shift, Alt, Super/Cmd, then the key.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        if (Modifiers.HasFlag(KeyModifiers.Control)) builder.Append("Ctrl+");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) builder.Append("Shift+");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) builder.Append("Alt+");
        if (Modifiers.HasFlag(KeyModifiers.Super)) builder.Append(SuperName).Append('+');
        return builder.Append(Key).ToString();
    }

    /// <summary>
    ///     The KeyDown path. Compares the whole modifier set rather than testing flags one
    ///     at a time: exact comparison is what makes Ctrl+Z and Ctrl+Shift+Z two separate
    ///     bindings instead of one that fires on both.
    /// </summary>
    public bool Matches(KeyboardKeyEventArgs e)
    {
        return e.Key == Key && (e.Modifiers & Relevant) == Modifiers;
    }

    /// <summary>The per-frame path: pressed this frame, with exactly these modifiers held.</summary>
    public bool IsPressed(KeyboardState state)
    {
        return state.IsKeyPressed(Key) && Held(state) == Modifiers;
    }

    /// <summary>
    ///     The per-frame held path, for keys that repeat while down.
    /// </summary>
    /// <param name="exact">
    ///     False lets modifiers the binding doesn't name through. The seek keys need it:
    ///     there Shift and Ctrl scale the step size rather than forming part of the binding.
    /// </param>
    public bool IsDown(KeyboardState state, bool exact = true)
    {
        if (!state.IsKeyDown(Key)) return false;
        var held = Held(state);
        return exact ? held == Modifiers : (held & Modifiers) == Modifiers;
    }

    /// <summary>
    ///     Rebuilds the modifier set from the key states, because
    ///     <see cref="KeyboardState" /> has no modifier bitfield of its own.
    /// </summary>
    private static KeyModifiers Held(KeyboardState state)
    {
        var modifiers = (KeyModifiers)0;
        if (state.IsKeyDown(Keys.LeftControl) || state.IsKeyDown(Keys.RightControl))
            modifiers |= KeyModifiers.Control;
        if (state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift))
            modifiers |= KeyModifiers.Shift;
        if (state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt))
            modifiers |= KeyModifiers.Alt;
        if (state.IsKeyDown(Keys.LeftSuper) || state.IsKeyDown(Keys.RightSuper))
            modifiers |= KeyModifiers.Super;
        return modifiers;
    }
}

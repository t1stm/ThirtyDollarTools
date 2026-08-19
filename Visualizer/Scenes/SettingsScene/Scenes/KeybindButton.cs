using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using VisualizerScene.Settings;

namespace SettingsScene.Scenes;

/// <summary>
///     One shortcut on the settings screen: shows what the action is bound to, and captures a
///     new combo when clicked. Escape cancels, Delete or Backspace puts the platform default
///     back - which is also the only escape hatch for someone who has bound themselves into a
///     corner, so the reset gesture wins over capturing those two keys.
/// </summary>
public sealed class KeybindButton : Button
{
    private readonly BindInfo _info;

    public KeybindButton(UIContext context, BindInfo info) : base(context, Keybinds.Get(info.Id).ToString())
    {
        _info = info;
        Classes = ["keybind-button"];
        Focusable = true;

        // Never unsubscribed: the settings screen is built during the boot preload and lives
        // as long as the process. Reset shortcuts is why this exists - it rewrites rows the
        // user is looking at but never touched.
        Keybinds.Changed += Refresh;
    }

    /// <summary>Which action this row is for. Read by the test that checks every one has a row.</summary>
    public Bind Id => _info.Id;

    protected override void FocusGained()
    {
        SetClass("keybind-button-capturing", true);
        Value = "Press a key...";
    }

    protected override void FocusLost()
    {
        Refresh();
    }

    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        // Unhandled Escape is what UIContext blurs on, and FocusLost puts the old label
        // back - so cancelling is one `return false` rather than its own branch.
        if (e.Key == Keys.Escape) return false;

        // Bare modifier presses are ignored, or holding Ctrl before pressing Z would commit
        // "Ctrl" on its own as the binding.
        if (IsModifier(e.Key)) return true;

        if (e.Key is Keys.Delete or Keys.Backspace)
        {
            Keybinds.Rebind(_info.Id, _info.Default);
            Context.Blur();
            return true;
        }

        var bind = Keybind.From(e);

        // Refuse rather than steal: last-writer-wins would silently unbind an unrelated
        // shortcut and the user would find out three sessions later. Cross-scene duplicates
        // are fine and expected - Space is play/pause on both screens.
        if (Keybinds.Conflict(_info.Id, bind, _info.Scene) is { } conflict)
        {
            SetClass("keybind-button-conflict", true);
            // ponytail: the message stays until the next keypress instead of timing out -
            // no clock to run, and nothing vanishes before it has been read.
            Value = $"Already used by \"{Keybinds.Info(conflict).Name}\"";
            return true;
        }

        Keybinds.Rebind(_info.Id, bind);
        Context.Blur();
        return true;
    }

    private static bool IsModifier(Keys key)
    {
        return key is Keys.LeftControl or Keys.RightControl or Keys.LeftShift or Keys.RightShift
            or Keys.LeftAlt or Keys.RightAlt or Keys.LeftSuper or Keys.RightSuper;
    }

    private void Refresh()
    {
        SetClass("keybind-button-capturing", false);
        SetClass("keybind-button-conflict", false);
        Value = Keybinds.Get(_info.Id).ToString();
    }
}

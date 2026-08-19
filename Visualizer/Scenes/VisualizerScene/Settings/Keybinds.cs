using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace VisualizerScene.Settings;

/// <summary>Which screen a binding belongs to. The same combo may mean different things on each.</summary>
public enum BindScene
{
    Visualizer,
    Editor
}

/// <summary>Every rebindable action. The name is the serialised id, so don't rename one lightly.</summary>
public enum Bind
{
    VisualizerBack,
    VisualizerPlayPause,
    VisualizerPlayPauseSilent,
    VisualizerTogglePlayerBar,
    VisualizerCycleCamera,
    VisualizerSeekBack,
    VisualizerSeekForward,
    VisualizerVolumeUp,
    VisualizerVolumeDown,
    VisualizerPreviousSequence,
    VisualizerNextSequence,
    VisualizerRestart,
    VisualizerRestartPaused,
    VisualizerReloadSequences,
    VisualizerZoomIn,
    VisualizerZoomOut,
    VisualizerToggleDebug,

    EditorUndo,
    EditorRedo,
    EditorRedoAlt,
    EditorCopy,
    EditorPaste,
    EditorCut,
    EditorSelectAll,
    EditorDelete,
    EditorDeleteAlt,
    EditorDrawTool,
    EditorSelectTool,
    EditorPlayPause,
    EditorRestart
}

/// <summary>One row of the shortcuts table: what the action is, and what it is bound to out of the box.</summary>
public sealed record BindInfo(Bind Id, BindScene Scene, string Name, string Description, Keybind Default);

/// <summary>
///     The shortcut table, shared by every screen that reads a key.
///     <para>
///         Static on purpose. The two scenes that consume it are built without a
///         <see cref="VisualizerSettings" /> in hand (<c>new Editor(game, workflow)</c>) and the real
///         consumers sit several levels down their UI trees, so threading a settings object to
///         them would be a ten-file diff that buys nothing over the static
///         <see cref="ThirtyDollarVisualizer.VisualizerSettings.SettingsHandler" /> the codebase
///         already has.
///     </para>
///     <para>
///         Read bindings at the point of use - <see cref="Get" /> per event or per frame. Caching
///         one into a field is the single refactor that would break rebinding on a running process.
///     </para>
/// </summary>
public static class Keybinds
{
    /// <summary>Ctrl everywhere except macOS, where the same shortcuts are Cmd.</summary>
    public static readonly KeyModifiers Primary =
        OperatingSystem.IsMacOS() ? KeyModifiers.Super : KeyModifiers.Control;

    /// <summary>What <see cref="Primary" /> is called, for hint text.</summary>
    public static readonly string PrimaryName = OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl";

    /// <summary>
    ///     Every action, in the order the settings screen lists it. The macOS requirement is
    ///     the whole of <see cref="Primary" /> appearing in the defaults below - a Mac's first
    ///     launch shows Cmd bindings without anything being written to disk, and a settings
    ///     file carried to a PC gets Ctrl back for everything the user never rebound.
    /// </summary>
    public static readonly BindInfo[] All =
    [
        new(Bind.VisualizerBack, BindScene.Visualizer, "Back to home",
            "Leave the visualizer.", new Keybind(Keys.Escape, 0)),
        new(Bind.VisualizerPlayPause, BindScene.Visualizer, "Play / pause",
            "Toggle playback.", new Keybind(Keys.Space, 0)),
        new(Bind.VisualizerPlayPauseSilent, BindScene.Visualizer, "Play / pause quietly",
            "Toggle playback without the status message.", new Keybind(Keys.Space, KeyModifiers.Shift)),
        new(Bind.VisualizerTogglePlayerBar, BindScene.Visualizer, "Toggle player bar",
            "Show or hide the bar along the bottom.", new Keybind(Keys.H, 0)),
        new(Bind.VisualizerCycleCamera, BindScene.Visualizer, "Cycle camera mode",
            "Step through the camera follow modes.", new Keybind(Keys.C, 0)),
        new(Bind.VisualizerSeekBack, BindScene.Visualizer, "Seek back",
            "Hold to rewind.\nShift is a tenth, Shift+Ctrl a hundredth.",
            new Keybind(Keys.Left, 0)),
        new(Bind.VisualizerSeekForward, BindScene.Visualizer, "Seek forward",
            "Hold to fast forward.\nShift is a tenth, Shift+Ctrl a hundredth.",
            new Keybind(Keys.Right, 0)),
        new(Bind.VisualizerVolumeUp, BindScene.Visualizer, "Volume up",
            "Hold to raise the volume.", new Keybind(Keys.Up, 0)),
        new(Bind.VisualizerVolumeDown, BindScene.Visualizer, "Volume down",
            "Hold to lower the volume.", new Keybind(Keys.Down, 0)),
        new(Bind.VisualizerPreviousSequence, BindScene.Visualizer, "Previous sequence",
            "Jump back to the previous sequence.", new Keybind(Keys.PageUp, 0)),
        new(Bind.VisualizerNextSequence, BindScene.Visualizer, "Next sequence",
            "Jump forward to the next sequence.", new Keybind(Keys.PageDown, 0)),
        new(Bind.VisualizerRestart, BindScene.Visualizer, "Restart",
            "Play the cover again from the beginning.", new Keybind(Keys.R, 0)),
        new(Bind.VisualizerRestartPaused, BindScene.Visualizer, "Restart paused",
            "Go back to the beginning and stay stopped.", new Keybind(Keys.R, KeyModifiers.Shift)),
        new(Bind.VisualizerReloadSequences, BindScene.Visualizer, "Reload sequences",
            "Re-read the loaded files from disk.",
            new Keybind(Keys.R, Primary | KeyModifiers.Shift)),
        new(Bind.VisualizerZoomIn, BindScene.Visualizer, "Zoom in",
            "Hold to move the camera closer.", new Keybind(Keys.Equal, Primary)),
        new(Bind.VisualizerZoomOut, BindScene.Visualizer, "Zoom out",
            "Hold to move the camera away.", new Keybind(Keys.Minus, Primary)),
        new(Bind.VisualizerToggleDebug, BindScene.Visualizer, "Toggle debug info",
            "Show frame timings and playback counters.", new Keybind(Keys.D, Primary)),

        new(Bind.EditorUndo, BindScene.Editor, "Undo",
            "Take back the last edit.", new Keybind(Keys.Z, Primary)),
        new(Bind.EditorRedo, BindScene.Editor, "Redo",
            "Put back an undone edit.", new Keybind(Keys.Z, Primary | KeyModifiers.Shift)),
        new(Bind.EditorRedoAlt, BindScene.Editor, "Redo (alternate)",
            "The other combo people reach for.", new Keybind(Keys.Y, Primary)),
        new(Bind.EditorCopy, BindScene.Editor, "Copy",
            "Copy the selection.", new Keybind(Keys.C, Primary)),
        new(Bind.EditorPaste, BindScene.Editor, "Paste",
            "Paste what was copied.", new Keybind(Keys.V, Primary)),
        new(Bind.EditorCut, BindScene.Editor, "Cut",
            "Copy the selection and remove it.", new Keybind(Keys.X, Primary)),
        new(Bind.EditorSelectAll, BindScene.Editor, "Select all",
            "Select everything in the view you are in.", new Keybind(Keys.A, Primary)),
        new(Bind.EditorDelete, BindScene.Editor, "Delete selection",
            "Remove the selected notes or clips.", new Keybind(Keys.Delete, 0)),
        new(Bind.EditorDeleteAlt, BindScene.Editor, "Delete selection (alternate)",
            "The other key people reach for.", new Keybind(Keys.Backspace, 0)),
        new(Bind.EditorDrawTool, BindScene.Editor, "Draw tool",
            "Switch to drawing notes.", new Keybind(Keys.D, 0)),
        new(Bind.EditorSelectTool, BindScene.Editor, "Select tool",
            "Switch to selecting notes.", new Keybind(Keys.E, 0)),
        new(Bind.EditorPlayPause, BindScene.Editor, "Play / pause",
            "Toggle playback.", new Keybind(Keys.Space, 0)),
        new(Bind.EditorRestart, BindScene.Editor, "Restart",
            "Play again from the beginning.", new Keybind(Keys.Space, KeyModifiers.Shift))
    ];

    private static readonly Dictionary<Bind, BindInfo> Table = All.ToDictionary(info => info.Id);

    /// <summary>Only what the user actually changed, so the settings line stays absent until they do.</summary>
    private static readonly Dictionary<Bind, Keybind> Overrides = [];

    private static VisualizerSettings? _settings;

    /// <summary>True while <see cref="Write" /> is pushing the string out, so the echo back is ignored.</summary>
    private static bool _writing;

    /// <summary>
    ///     Fires after the table settles - a rebind, a reset, or a re-parse driven by the
    ///     settings object. Anything that renders a binding as text has to redraw here; the
    ///     consumers that read a key resolve it at the point of use and need nothing.
    /// </summary>
    public static event Action? Changed;

    public static Keybind Get(Bind id)
    {
        return Overrides.TryGetValue(id, out var bind) ? bind : Table[id].Default;
    }

    public static BindInfo Info(Bind id)
    {
        return Table[id];
    }

    /// <summary>The action bound to this key event on the given screen, if any.</summary>
    // ponytail: linear scan of ~30 entries per key event. A lookup keyed by (Key, Modifiers)
    // if a profiler ever cares - it won't, this runs once per keystroke.
    public static Bind? Match(KeyboardKeyEventArgs e, BindScene scene)
    {
        foreach (var info in All)
            if (info.Scene == scene && Get(info.Id).Matches(e))
                return info.Id;
        return null;
    }

    /// <summary>Ctrl held, or Cmd on macOS. For the mouse gestures, which aren't rebindable.</summary>
    public static bool PrimaryDown(KeyboardState state)
    {
        return OperatingSystem.IsMacOS()
            ? state.IsKeyDown(Keys.LeftSuper) || state.IsKeyDown(Keys.RightSuper)
            : state.IsKeyDown(Keys.LeftControl) || state.IsKeyDown(Keys.RightControl);
    }

    /// <summary>Same question, asked of a key event.</summary>
    public static bool PrimaryHeld(KeyboardKeyEventArgs e)
    {
        return (e.Modifiers & Primary) != 0;
    }

    /// <summary>
    ///     The action in <paramref name="scene" /> that already holds <paramref name="bind" />,
    ///     ignoring <paramref name="id" /> itself. Cross-scene duplicates are fine and expected.
    /// </summary>
    public static Bind? Conflict(Bind id, Keybind bind, BindScene scene)
    {
        return All.FirstOrDefault(info => info.Scene == scene && info.Id != id && Get(info.Id) == bind)?.Id;
    }

    public static void Rebind(Bind id, Keybind bind)
    {
        if (bind == Table[id].Default) Overrides.Remove(id);
        else Overrides[id] = bind;
        Write();
    }

    public static void ResetToDefaults(BindScene scene)
    {
        foreach (var info in All)
            if (info.Scene == scene)
                Overrides.Remove(info.Id);
        Write();
    }

    /// <summary>
    ///     Points the table at a settings object: reads what it holds now, and follows it from
    ///     here. Called once from Program.cs, and again per test.
    /// </summary>
    public static void Attach(VisualizerSettings settings)
    {
        if (_settings != null) _settings.Changed -= OnSettingsChanged;
        _settings = settings;
        settings.Changed += OnSettingsChanged;

        Parse(settings.Keybinds);
        Changed?.Invoke();
    }

    /// <summary>
    ///     One line, because the settings file is line-based: "Undo:Ctrl+Z;Redo:Ctrl+Shift+Z".
    ///     Neither separator collides with the file's parser or with <see cref="Keybind.ToString" />.
    /// </summary>
    public static string Serialize()
    {
        return string.Join(';', Overrides.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
    }

    /// <summary>
    ///     Reads that string back. Unknown ids and unparseable bindings are skipped rather than
    ///     thrown on - the same contract the rest of the settings file has.
    /// </summary>
    public static void Deserialize(string? value)
    {
        Parse(value);
        Changed?.Invoke();
    }

    private static void Parse(string? value)
    {
        Overrides.Clear();
        if (string.IsNullOrWhiteSpace(value)) return;

        foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = entry.Split(':', 2);
            if (split.Length != 2) continue;
            if (!Enum.TryParse<Bind>(split[0].Trim(), out var id) || !Table.ContainsKey(id)) continue;
            if (!Keybind.TryParse(split[1], out var bind)) continue;
            Overrides[id] = bind;
        }
    }

    /// <summary>
    ///     Pushes the table into the settings string, then announces the change. The guard is
    ///     what keeps <see cref="Changed" /> firing once per rebind rather than twice - the
    ///     write comes back through <see cref="OnSettingsChanged" />, and re-parsing our own
    ///     output would say nothing new.
    /// </summary>
    private static void Write()
    {
        if (_settings != null)
        {
            _writing = true;
            try
            {
                _settings.Keybinds = Serialize();
            }
            finally
            {
                _writing = false;
            }
        }

        Changed?.Invoke();
    }

    private static void OnSettingsChanged(string name)
    {
        if (_writing || name != nameof(VisualizerSettings.Keybinds)) return;
        Parse(_settings?.Keybinds);
        Changed?.Invoke();
    }
}

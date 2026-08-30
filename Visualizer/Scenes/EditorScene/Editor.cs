using EditorScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Engine;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using VisualizerScene.Settings;

namespace EditorScene;

[PreloadGraphicsContext]
public class Editor : Scene, IFadeInScene
{
    private readonly DollarStoreCamera _camera;
    private readonly ThirtyDollarWorkflow _workflow;

    private UIContext _context;
    private EditorInterface _editorInterface;

    private CursorType _cursorType = CursorType.Default;
    private Vector2 _lastScale = Vector2.One;

    /// <param name="initialFile">
    ///     A project or sequence to open on boot, from <c>--mode editor -i</c>. Handled as
    ///     if it had been dropped on the window - see <see cref="FileDrop" />.
    /// </param>
    public Editor(Game game, ThirtyDollarWorkflow workflow, string? initialFile = null) : base(game)
    {
        _workflow = workflow;
        var clientSize = game.ClientSize;
        if (game.TryGetScreenScale(out var scaleX, out var scaleY))
            _lastScale = new Vector2(scaleX, scaleY);

        _camera = new DollarStoreCamera(Vector3.Zero, clientSize);
        _context = NewContext();

        _editorInterface = BuildInterface(_context);
        _editorInterface.Resize(clientSize.X, clientSize.Y);

        // Last: a sequence raises the same import dialog a drop does, and that needs the
        // interface already laid out to attach to.
        if (initialFile != null) FileDrop([initialFile]);
    }

    private UIContext NewContext()
    {
        return new UIContext
        {
            Camera = _camera,
            PixelScale = _lastScale,
            RequestCursor = type => _cursorType = type
        };
    }

    private EditorInterface BuildInterface(UIContext context)
    {
        return new EditorInterface(context, _workflow, () =>
        {
            _editorInterface?.Playback.Stop();
            Game.SceneManager.TransitionTo("home");
        });
    }

    /// <summary>
    ///     Rebuilds the whole editor from its markup, recompiling the logic blocks whose
    ///     scripts changed. Starts from a blank <c>EditorState</c>, so any open project is
    ///     lost - the state lives inside the interface being replaced. Stylesheet edits go
    ///     through <see cref="ReloadStyles" />, which keeps everything.
    /// </summary>
    public override void ReloadUI()
    {
        var context = NewContext();

        // Stop before dropping the tree that owns it: nothing would be left to release
        // playback's audio channels afterwards.
        _editorInterface.Playback.Stop();

        var ui = BuildInterface(context);
        ui.Alpha = _editorInterface.Alpha;

        _context = context;
        _editorInterface = ui;
        Resize(Game.ClientSize.X, Game.ClientSize.Y);
    }

    public override void ReloadStyles()
    {
        _editorInterface.Component.ReloadStyleSheet();
        _editorInterface.Resize(Game.ClientSize.X, Game.ClientSize.Y);
    }

    /// <summary>
    ///     Scene-wide opacity, driven by the loading screen when the boot hands off here.
    ///     1 on every entry from the home screen.
    /// </summary>
    public float InterfaceAlpha
    {
        get => _editorInterface.Alpha;
        set => _editorInterface.Alpha = value;
    }

    public override void Initialize(InitArguments initArguments)
    {
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments renderArgs)
    {
        _context.Render();
    }

    public override void TransitionedTo()
    {
        _editorInterface.SceneShown();
        Game.OnWindowActionUnavailable = message =>
            MessageDialog.Show(_context, _editorInterface.RootPanel, message);
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _cursorType = CursorType.Default;
        _editorInterface.Update(_context);

        var cursor = _cursorType switch
        {
            CursorType.Default => MouseCursor.Default,
            CursorType.Pointer => MouseCursor.PointingHand,
            CursorType.Text => MouseCursor.IBeam,
            CursorType.ResizeX => MouseCursor.ResizeEW,
            CursorType.ResizeY => MouseCursor.ResizeNS,
            _ => MouseCursor.Default
        };

        if (Game.Cursor != cursor)
            Game.Cursor = cursor;
    }

    public override void Resize(int w, int h)
    {
        float width = w;
        float height = h;
        if (Game.TryGetScreenScale(out var scaleX, out var scaleY))
        {
            width /= scaleX;
            height /= scaleY;
            _lastScale = new Vector2(scaleX, scaleY);
        }

        _camera.Viewport = new Vector2i((int)width, (int)height);
        _camera.UpdateMatrix();
        _context.PixelScale = _lastScale;

        _editorInterface.Resize(width, height);
    }

    public override void Shutdown()
    {
    }

    public override void FileDrop(string[] locations)
    {
        var project = locations.FirstOrDefault(l => l.EndsWith(".tdwproj", StringComparison.OrdinalIgnoreCase));
        if (project != null)
        {
            _editorInterface.LoadProjectFile(project);
            return;
        }

        // Only the first sequence file of a multi-drop is imported; batch import is out
        // of scope.
        var sequence = locations.FirstOrDefault(l =>
            l.EndsWith(".tdw", StringComparison.OrdinalIgnoreCase) ||
            l.EndsWith(".🗿", StringComparison.OrdinalIgnoreCase) ||
            l.EndsWith(".moai", StringComparison.OrdinalIgnoreCase));
        if (sequence != null)
        {
            _editorInterface.ImportSequenceFile(sequence);
            return;
        }

        // Extension-less files are usually TDW sequences saved without one, but they could be
        // anything - File.Exists keeps dropped folders out of the prompt.
        var extensionless = locations.FirstOrDefault(l =>
            string.IsNullOrEmpty(Path.GetExtension(l)) && File.Exists(l));
        if (extensionless != null) _editorInterface.ConfirmImportSequenceFile(extensionless);
    }

    public override void Keyboard(KeyboardState state)
    {
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        // Runs every frame (unlike Keyboard, which only fires while a key is down),
        // so modifier releases are seen too.
        _editorInterface.SetModifiers(
            keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift),
            Keybinds.PrimaryDown(keyboardState));
        _editorInterface.SetSpaceHeld(keyboardState.IsKeyDown(Keys.Space));
        _editorInterface.MouseEvent(mouseState, _lastScale);
    }

    public override void TextInput(TextInputEventArgs e)
    {
        _context.DispatchTextInput(e);
    }

    public override void KeyDown(KeyboardKeyEventArgs e)
    {
        if (_context.DispatchKeyDown(e)) return;
        var state = _editorInterface.State;

        // Escape isn't a shortcut but a fallthrough chain - clear selection, close modal,
        // close track, back - so it sits ahead of the bind table and isn't rebindable.
        if (e.Key == Keys.Escape)
        {
            if (state.SelectedNotes.Count > 0 || state.SelectedPlacements.Count > 0) state.ClearSelection();
            else if (!_editorInterface.TryCloseTopModal())
            {
                if (state.OpenedTrack != null) state.CloseTrack();
                else _editorInterface.RequestBack();
            }

            return;
        }

        // A dialog is modal: its focused inputs already took what they wanted above, and
        // nothing else may reach the editor behind it. Escape stays ahead of this.
        if (_editorInterface.HasOpenModal) return;

        // A focused TextInput lets modified combos fall through (see
        // TextInput.HandleKeyDown), so the clipboard binds skip while one is focused
        // rather than fighting the field's own editing keys.
        var textFocused = _context.FocusedElement is Sundex.Components.Inputs.TextInput;

        // The event's own modifier set, not last frame's: Mouse() refreshes these once a
        // frame, so a Ctrl+Arrow arriving in the same frame as the Ctrl press would read as
        // a plain arrow to whoever asks (the faithful sequence does).
        _editorInterface.SetModifiers((e.Modifiers & KeyModifiers.Shift) != 0, Keybinds.PrimaryHeld(e));

        // The Draw tool's plain D is an exact-modifier match, so Ctrl+D stays free for a
        // future "duplicate selection".
        switch (Keybinds.Match(e, BindScene.Editor))
        {
            case Bind.EditorUndo:
                state.Undo();
                return;
            case Bind.EditorRedo or Bind.EditorRedoAlt:
                state.Redo();
                return;
            case Bind.EditorDrawTool:
                state.ActiveTool = EditorTool.Draw;
                return;
            case Bind.EditorSelectTool:
                state.ActiveTool = EditorTool.Select;
                return;
            case Bind.EditorCopy when !textFocused:
                state.CopySelection();
                return;
            case Bind.EditorPaste when !textFocused:
                state.Paste();
                return;
            case Bind.EditorCut when !textFocused:
                state.CutSelection();
                return;
            case Bind.EditorSelectAll when !textFocused:
                state.SelectAll();
                return;
            // Space is the faithful sequence's move modifier while it has a selection, so
            // it must not also start playback on the same press.
            case Bind.EditorPlayPause when !_editorInterface.SpaceMovesSelection:
                _editorInterface.Playback.PlayPause();
                return;
            case Bind.EditorRestart:
                _editorInterface.Playback.Restart();
                return;
            // Not guarded on textFocused: a focused field swallows plain keys itself and
            // deliberately passes modified combos through, and none of these mean anything
            // inside a text field.
            case Bind.EditorSave:
                _editorInterface.SaveProject();
                return;
            case Bind.EditorOpen:
                _editorInterface.ShowLoadDialog();
                return;
            case Bind.EditorNew:
                _editorInterface.NewProject();
                return;
            case Bind.EditorNudgeLeft:
                _editorInterface.NudgeSelection(-1, 0);
                return;
            case Bind.EditorNudgeRight:
                _editorInterface.NudgeSelection(1, 0);
                return;
            case Bind.EditorNudgeUp:
                _editorInterface.NudgeSelection(0, 1);
                return;
            case Bind.EditorNudgeDown:
                _editorInterface.NudgeSelection(0, -1);
                return;
            case Bind.EditorToggleMute:
                ToggleChannels(state, state.ToggleMute);
                return;
            case Bind.EditorToggleSolo:
                ToggleChannels(state, state.ToggleSolo);
                return;
        }

        // Keys that only mean something with a faithful track open, and only after the bind
        // table has passed: the arrows (which match no bind while a modifier is held),
        // Delete, Enter and Tab.
        _editorInterface.FaithfulKeyDown(e);
    }

    /// <summary>
    ///     The keyboard's equivalent of the lane header's M/S toggles: acts on every
    ///     channel the selected clips sit on, once each (a multi-clip selection can share
    ///     a lane, and toggling the same channel twice would cancel itself out).
    /// </summary>
    private static void ToggleChannels(EditorState state, Action<int> toggle)
    {
        foreach (var channel in state.SelectedPlacements.Select(p => p.Channel).Distinct().ToArray())
            toggle(channel);
    }
}
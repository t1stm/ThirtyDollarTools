using EditorScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Sundex.Components.Abstractions;
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

        // Last: a sequence puts up the same import dialog a drop does, which needs the
        // interface laid out to have somewhere to go.
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
    ///     Rebuilds the whole editor from its markup. This is the expensive reload - the
    ///     editor's tree is the largest in the program and its logic blocks recompile when
    ///     their scripts change - and it starts from a blank <c>EditorState</c>: whatever
    ///     project was open is gone, because the state lives inside the interface being
    ///     replaced. Editing a stylesheet takes <see cref="ReloadStyles" /> instead, which
    ///     keeps all of it.
    /// </summary>
    public override void ReloadUI()
    {
        var context = NewContext();

        // Stopped before the tree that owns it is dropped: playback holds audio channels
        // that nothing would be left to stop afterwards.
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

        // Only the first sequence file of a multi-drop is handled, matching the
        // FirstOrDefault behavior above. Batch import is out of scope.
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

        // A focused TextInput deliberately lets modified combos fall through (see
        // TextInput.HandleKeyDown) - a future TextInput copy/paste must not fight the
        // editor clipboard, so the clipboard binds skip while one is focused.
        var textFocused = _context.FocusedElement is Sundex.Components.Inputs.TextInput;

        // Ctrl+D stays free for a future "duplicate selection": the Draw tool's plain D is
        // an exact-modifier match, so it no longer needs a guard saying so.
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
            case Bind.EditorPlayPause:
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
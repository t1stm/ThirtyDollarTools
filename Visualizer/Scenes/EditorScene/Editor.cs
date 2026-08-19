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
public class Editor : Scene
{
    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;
    private readonly EditorInterface _editorInterface;

    private CursorType _cursorType = CursorType.Default;
    private Vector2 _lastScale = Vector2.One;

    public Editor(Game game, ThirtyDollarWorkflow workflow) : base(game)
    {
        var clientSize = game.ClientSize;
        if (game.TryGetScreenScale(out var scaleX, out var scaleY))
            _lastScale = new Vector2(scaleX, scaleY);

        _camera = new DollarStoreCamera(Vector3.Zero, clientSize);
        _context = new UIContext
        {
            Camera = _camera,
            PixelScale = _lastScale,
            RequestCursor = type => _cursorType = type
        };

        _editorInterface = new EditorInterface(_context, workflow, () =>
        {
            _editorInterface?.Playback.Stop();
            Game.SceneManager.TransitionTo("home");
        });
        _editorInterface.Resize(clientSize.X, clientSize.Y);
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
        }
    }
}
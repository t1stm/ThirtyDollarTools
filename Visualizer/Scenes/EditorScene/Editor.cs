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
            keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl));
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
        switch (e.Key)
        {
            // Escape clears the selection first; only once nothing is selected does it
            // fall through to the existing chain (close modal → close track → back).
            case Keys.Escape when state.SelectedNotes.Count > 0 || state.SelectedPlacements.Count > 0:
                state.ClearSelection();
                return;
            case Keys.Escape when _editorInterface.TryCloseTopModal():
                return;
            case Keys.Escape:
            {
                if (state.OpenedTrack != null) state.CloseTrack();
                else _editorInterface.RequestBack();
                return;
            }
            case Keys.Z when e.Modifiers.HasFlag(KeyModifiers.Control):
            {
                if (e.Modifiers.HasFlag(KeyModifiers.Shift)) state.Redo();
                else state.Undo();
                return;
            }
            case Keys.Y when e.Modifiers.HasFlag(KeyModifiers.Control):
                state.Redo();
                return;
            // Plain letters only: Ctrl+D is reserved for the future "duplicate selection".
            case Keys.D when !e.Modifiers.HasFlag(KeyModifiers.Control):
                state.ActiveTool = EditorTool.Draw;
                return;
            case Keys.E when !e.Modifiers.HasFlag(KeyModifiers.Control):
                state.ActiveTool = EditorTool.Select;
                return;
        }

        // A focused TextInput deliberately lets Ctrl-combos fall through (see
        // TextInput.HandleKeyDown) - a future TextInput copy/paste must not fight the
        // editor clipboard, so the fallback below skips while one is focused.
        if (_context.FocusedElement is not Sundex.Components.Inputs.TextInput &&
            e.Modifiers.HasFlag(KeyModifiers.Control))
            switch (e.Key)
            {
                case Keys.C:
                    state.CopySelection();
                    return;
                case Keys.V:
                    state.Paste();
                    return;
                case Keys.X:
                    state.CutSelection();
                    return;
                case Keys.A:
                    state.SelectAll();
                    return;
            }

        if (e.Key != Keys.Space) return;

        if (e.Modifiers.HasFlag(KeyModifiers.Shift)) _editorInterface.Playback.Restart();
        else _editorInterface.Playback.PlayPause();
    }
}
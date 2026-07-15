using EditorScene.Scenes;
using OpenTK.Mathematics;
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

    public Editor(Game game) : base(game)
    {
        var clientSize = game.ClientSize;
        if (game.TryGetScreenScale(out var scaleX, out var scaleY))
        {
            _lastScale = new Vector2(scaleX, scaleY);
            clientSize.X = (int)(clientSize.X / scaleX);
            clientSize.Y = (int)(clientSize.Y / scaleY);
        }

        _camera = new DollarStoreCamera(Vector3.Zero, clientSize);
        _context = new UIContext
        {
            Camera = _camera,
            RequestCursor = type => _cursorType = type
        };

        _editorInterface = new EditorInterface(_context,
            () => { Game.SceneManager.TransitionTo("home"); });
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

        _editorInterface.Resize();
    }

    public override void Shutdown()
    {
    }

    public override void FileDrop(string[] locations)
    {
    }

    public override void Keyboard(KeyboardState state)
    {
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        _editorInterface.MouseEvent(mouseState, _lastScale);
    }
}
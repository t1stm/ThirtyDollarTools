using HomeScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;

namespace HomeScene;

[PreloadGraphicsContext]
public class Home : Scene
{
    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;

    private readonly HomeInterface _homeInterface;

    private CursorType _cursorType = CursorType.Normal;
    private Vector2 _lastScale = Vector2.One;

    public Home(SceneManager sceneManager) : base(sceneManager)
    {
        var clientSize = sceneManager.Game.ClientSize;
        if (sceneManager.Game.TryGetScreenScale(out var scaleX, out var scaleY))
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

        _homeInterface = new HomeInterface(_context,
            () => { sceneManager.Game.Enqueue(instance => instance.SceneManager.TransitionTo("visualizer")); },
            () => { sceneManager.Game.Enqueue(instance => instance.SceneManager.TransitionTo("drum-master")); },
            () => { sceneManager.Game.Enqueue(instance => instance.SceneManager.TransitionTo("editor")); },
            () => { sceneManager.Game.Enqueue(instance => instance.SceneManager.TransitionTo("settings")); });
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
        _cursorType = CursorType.Normal;
        var mouseState = Game.MouseState;

        _homeInterface.Update(_context, mouseState, _lastScale);

        Game.Cursor = _cursorType switch
        {
            CursorType.Normal => MouseCursor.Default,
            CursorType.Pointer => MouseCursor.PointingHand,
            CursorType.ResizeX => MouseCursor.ResizeEW,
            CursorType.ResizeY => MouseCursor.ResizeNS,
            _ => MouseCursor.Default
        };
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

        _homeInterface.Resize();
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
    }
}
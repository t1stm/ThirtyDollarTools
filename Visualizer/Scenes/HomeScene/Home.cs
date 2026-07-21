using HomeScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Engine;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Enums;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using Sundex.Engine.Text;

namespace HomeScene;

[PreloadGraphicsContext]
public class Home : Scene
{
    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;

    private readonly HomeInterface _homeInterface;
    private readonly TextBuffer _versionBuffer;
    private readonly TextSlice _versionNote;

    private CursorType _cursorType = CursorType.Default;
    private Vector2 _lastScale = Vector2.One;

    public Home(Game game, string version) : base(game)
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

        _homeInterface = new HomeInterface(_context,
            () => { Game.SceneManager.TransitionTo("visualizer"); },
            () => { Game.SceneManager.TransitionTo("drum-master"); },
            () => { Game.SceneManager.TransitionTo("editor"); },
            () => { Game.SceneManager.TransitionTo("settings"); });

        _versionBuffer = new TextBuffer(_context.TextProvider, _context.DeleteQueue);
        _versionNote = _versionBuffer.GetTextSlice(
                $"""
                 Check regularly for updates at:
                 https://github.com/t1stm/ThirtyDollarTools

                 Current Version: {version}
                 """,
                (value, buffer, range) => new TextSlice(buffer, range)
                {
                    Value = value,
                    FontSize = 14
                })
            .WithPosition((10, clientSize.Y, 0), PositionAlign.Bottom | PositionAlign.Left);
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
        _versionBuffer.RenderBuffer(_camera);
    }

    public override void TransitionedTo()
    {
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _cursorType = CursorType.Default;
        _homeInterface.Update(_context);

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

        _homeInterface.Resize();
        _versionNote.SetPosition((10, height, 0), PositionAlign.Bottom | PositionAlign.Left);
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
        _homeInterface.MouseEvent(mouseState, _lastScale);
    }
}
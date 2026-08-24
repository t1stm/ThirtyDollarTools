using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SettingsScene.Scenes;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Engine;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;
using VisualizerScene.Settings;

namespace SettingsScene;

[PreloadGraphicsContext]
public class Settings : Scene
{
    private readonly DollarStoreCamera _camera;
    private readonly VisualizerSettings _settings;

    private UIContext _context;
    private SettingsInterface _settingsInterface;

    private CursorType _cursorType = CursorType.Default;
    private Vector2 _lastScale = Vector2.One;

    public Settings(Game game, VisualizerSettings settings) : base(game)
    {
        _settings = settings;
        var clientSize = game.ClientSize;
        if (game.TryGetScreenScale(out var scaleX, out var scaleY))
            _lastScale = new Vector2(scaleX, scaleY);

        _camera = new DollarStoreCamera(Vector3.Zero, clientSize);
        _context = NewContext();

        _settingsInterface = BuildInterface(_context);
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

    private SettingsInterface BuildInterface(UIContext context)
    {
        return new SettingsInterface(context, _settings,
            () => { Game.SceneManager.TransitionTo("home"); });
    }

    public override void ReloadUI()
    {
        // Built into a context of its own and assigned only once it stands: a markup file
        // saved halfway through an edit throws in here, and this way the screen keeps the
        // UI it already had instead of being left half-torn-down. The old context takes
        // the old tree's render queue, hover chain and focus with it.
        var context = NewContext();
        var ui = BuildInterface(context);

        _context = context;
        _settingsInterface = ui;
        Resize(Game.ClientSize.X, Game.ClientSize.Y);
    }

    public override void ReloadStyles()
    {
        _settingsInterface.Component.ReloadStyleSheet();
        _settingsInterface.Resize();
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
        _settingsInterface.Update(_context);

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

        _settingsInterface.Resize();
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

    public override void TextInput(TextInputEventArgs e)
    {
        _context.DispatchTextInput(e);
    }

    public override void KeyDown(KeyboardKeyEventArgs e)
    {
        _context.DispatchKeyDown(e);
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
        _settingsInterface.MouseEvent(mouseState, _lastScale);
    }
}
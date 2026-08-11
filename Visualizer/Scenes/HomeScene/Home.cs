using HomeScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Updates;
using Sundex.Components.Abstractions;
using Sundex.Engine;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Enums;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;

namespace HomeScene;

[PreloadGraphicsContext]
public class Home : Scene
{
    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;

    private readonly HomeInterface _homeInterface;

    private CursorType _cursorType = CursorType.Default;
    private Vector2 _lastScale = Vector2.One;
    private bool _updateNoteShown;

    /// <param name="checkingForUpdates">
    ///     Whether the update check runs. When it does, the "check regularly" line is dropped -
    ///     the program is doing the checking, and the note is replaced by what it finds.
    /// </param>
    public Home(Game game, string version, bool checkingForUpdates) : base(game)
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

        _homeInterface.VersionLabel.SetTextContents(version);

        // Nothing to say while the check is still out: the markup's line tells a build that
        // isn't checking where to look, and would be wrong here.
        if (checkingForUpdates) _homeInterface.UpdateLabel.Visible = false;
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
        _homeInterface.PlayIntro();
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _cursorType = CursorType.Default;
        _homeInterface.Update(_context);

        // The check runs on the loading screen, but it's over the network - it can land
        // after this scene is built, so the note is written when it does. Nothing is
        // written while it's still running, or when it found nothing newer.
        if (!_updateNoteShown && (UpdateChecker.Available is not null || UpdateChecker.Failed))
        {
            _updateNoteShown = true;
            var available = UpdateChecker.Available;

            _homeInterface.UpdateLabel.SetTextContents(available is { } release
                ? $"{release.TagName} is out - {release.HtmlUrl}"
                : "Update check failed. See the log for details.");
            _homeInterface.UpdateLabel.SetClass("note-attention", available is not null);
            _homeInterface.UpdateLabel.Visible = true;
        }

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
        _homeInterface.MouseEvent(mouseState, _lastScale);
    }
}

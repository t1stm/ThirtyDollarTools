using HomeScene.Scenes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Updates;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Engine;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Enums;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;

namespace HomeScene;

[PreloadGraphicsContext]
public class Home : Scene, IFadeInScene
{
    private readonly DollarStoreCamera _camera;
    private readonly Func<bool> _checkingForUpdates;
    private readonly string _version;

    private UIContext _context;
    private HomeInterface _homeInterface;

    private CursorType _cursorType = CursorType.Default;
    private Vector2 _lastScale = Vector2.One;
    private bool _updateNoteShown;

    /// <param name="checkingForUpdates">
    ///     Whether the update check runs. When it does, the "check regularly" line is dropped
    ///     and replaced by whatever the check finds. Must stay a callback: this scene is built
    ///     during the boot, before the setting has been answered or the settings screen can
    ///     change it, so a copy taken here would be stale.
    /// </param>
    public Home(Game game, string version, Func<bool> checkingForUpdates) : base(game)
    {
        _checkingForUpdates = checkingForUpdates;
        _version = version;
        var clientSize = game.ClientSize;
        if (game.TryGetScreenScale(out var scaleX, out var scaleY))
            _lastScale = new Vector2(scaleX, scaleY);

        _camera = new DollarStoreCamera(Vector3.Zero, clientSize);
        _context = NewContext();
        _homeInterface = BuildInterface(_context);
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

    private HomeInterface BuildInterface(UIContext context)
    {
        var ui = new HomeInterface(context,
            () => { Game.SceneManager.TransitionTo("visualizer"); },
            () => { Game.SceneManager.TransitionTo("drum-master"); },
            () => { Game.SceneManager.TransitionTo("editor"); },
            () => { Game.SceneManager.TransitionTo("settings"); });

        ui.VersionLabel.SetTextContents(_version);

        // Nothing to say while the check is still out: the markup's line tells a build that
        // isn't checking where to look, and would be wrong here.
        ui.UpdateLabel.Visible = !_checkingForUpdates();
        return ui;
    }

    public override void ReloadUI()
    {
        var context = NewContext();
        var ui = BuildInterface(context);

        // Update() writes the note only on the edge, so a rebuilt tree has to be flagged as
        // not yet carrying it.
        ui.Alpha = _homeInterface.Alpha;
        _updateNoteShown = false;

        _context = context;
        _homeInterface = ui;
        Resize(Game.ClientSize.X, Game.ClientSize.Y);
    }

    public override void ReloadStyles()
    {
        _homeInterface.Component.ReloadStyleSheet();
        _homeInterface.Resize();
    }

    /// <summary>
    ///     Scene-wide opacity, driven from 0 to 1 by the loading screen as it fades itself
    ///     off over this one. 1 on every later entry.
    /// </summary>
    public float InterfaceAlpha
    {
        get => _homeInterface.Alpha;
        set => _homeInterface.Alpha = value;
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
        Game.OnWindowActionUnavailable = message =>
            MessageDialog.Show(_context, _homeInterface.RootPanel, message);
    }

    public override void Update(UpdateArguments updateArgs)
    {
        _cursorType = CursorType.Default;
        _homeInterface.Update(_context);

        // The check starts on the loading screen but can land after this scene is built, so
        // the note is written when it does. Nothing is written while it is still running, or
        // when it found nothing newer.
        if (!_updateNoteShown)
            _homeInterface.UpdateLabel.Visible = !_checkingForUpdates();

        if (!_updateNoteShown && (UpdateChecker.Available is not null || UpdateChecker.Failed))
        {
            _updateNoteShown = true;
            var available = UpdateChecker.Available;

            _homeInterface.UpdateLabel.SetTextContents(available != null
                ? $"{available.TagName} is out - {available.HtmlUrl}"
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
        // Nothing is clickable until the screen is fully up: this scene renders under the
        // loading screen for the length of the entrance fade.
        if (_homeInterface.Alpha < 1f) return;
        _homeInterface.MouseEvent(mouseState, _lastScale);
    }
}

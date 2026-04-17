using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Scenes.Arguments;
using Sundex.Engine.Threading;

namespace Sundex.Engine.Scenes;

public abstract class Scene(Game game)
{
    public Game Game { get; } = game;
    public AssetProvider AssetProvider => Game.AssetProvider;
    public SceneManager SceneManager => Game.SceneManager;
    public ThreadRunner ThreadRunner => Game.ThreadRunner;
    public ILogger Logger => Game.Logger;


    /// <summary>
    ///     Method called during the OnLoad procedure.
    /// </summary>
    public abstract void Initialize(InitArguments initArguments);

    /// <summary>
    ///     Method called by the manager after all scenes are loaded.
    /// </summary>
    public abstract void Start();

    /// <summary>
    ///     Method called by the render loop.
    /// </summary>
    public abstract void Render(RenderArguments renderArgs);

    /// <summary>
    ///     Method called when the scene is transitioned to.
    /// </summary>
    public abstract void TransitionedTo();

    /// <summary>
    ///     Method called by the update loop.
    /// </summary>
    public abstract void Update(UpdateArguments updateArgs);

    /// <summary>
    ///     Method called when the window is resized.
    /// </summary>
    /// <param name="w">The width of the resized window.</param>
    /// <param name="h">The height of the resized window.</param>
    public abstract void Resize(int w, int h);

    /// <summary>
    ///     Method that should be called when releasing scene resources.
    /// </summary>
    public abstract void Shutdown();

    /// <summary>
    ///     Triggered when a file is dropped on the window.
    /// </summary>
    /// <param name="locations">The location of the dropped files.</param>
    public abstract void FileDrop(string[] locations);

    /// <summary>
    ///     Event triggered when a keyboard button is pressed.
    /// </summary>
    /// <param name="state">A copy of the KeyboardState.</param>
    public abstract void Keyboard(KeyboardState state);

    /// <summary>
    ///     Event triggered when a pointing device is interacted with.
    /// </summary>
    /// <param name="mouseState">A copy of the MouseState.</param>
    /// <param name="keyboardState">A copy of the KeyboardState.</param>
    public abstract void Mouse(MouseState mouseState, KeyboardState keyboardState);
}
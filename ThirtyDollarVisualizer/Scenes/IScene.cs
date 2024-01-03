using OpenTK.Windowing.GraphicsLibraryFramework;

namespace ThirtyDollarVisualizer.Scenes;

public interface IScene
{
    /// <summary>
    /// Method called during the OnLoad procedure.
    /// </summary>
    /// <param name="manager"></param>
    public void Init(Manager manager);
    /// <summary>
    /// Method called by the manager after all scenes are loaded.
    /// </summary>
    public void Start();
    /// <summary>
    /// Method called by the render loop.
    /// </summary>
    public void Render();
    /// <summary>
    /// Method called by the update loop.
    /// </summary>
    public void Update();
    /// <summary>
    /// Method called when the window is resized.
    /// </summary>
    /// <param name="w">The width of the resized window.</param>
    /// <param name="h">The height of the resized window.</param>
    public void Resize(int w, int h);
    /// <summary>
    /// Method called before the main window closes.
    /// </summary>
    public void Close();
    
    /// <summary>
    /// Triggered when a file is dropped on the window.
    /// </summary>
    /// <param name="location">The location of the dropped file.</param>
    public void FileDrop(string location);

    /// <summary>
    /// Event triggered when a keyboard button is pressed.
    /// </summary>
    /// <param name="state">A copy of the KeyboardState.</param>
    public void Keyboard(KeyboardState state);

    /// <summary>
    /// Event triggered when a pointing device is interacted with.
    /// </summary>
    /// <param name="state">A copy of the MouseState.</param>
    public void Mouse(MouseState state);
}
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;

namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract;

/// <summary>
/// Represents an interface for objects that can be rendered using a camera in the rendering engine.
/// </summary>
public interface IRenderable
{
    /// <summary>
    /// Renders the object using the specified camera.
    /// </summary>
    /// <param name="camera">The camera to use for rendering the object.</param>
    public void Render(Camera camera);
}
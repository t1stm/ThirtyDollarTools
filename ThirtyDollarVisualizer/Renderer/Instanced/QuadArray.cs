using ThirtyDollarVisualizer.Objects;

namespace ThirtyDollarVisualizer.Renderer.Instanced;

/// <summary>
/// Represents a collection of quads that can only be allocated once.
/// When the size is changed due to user requirements the allocation must happen with a new object.
/// </summary>
public class QuadArray
{
    
    
    
    /// <summary>
    /// Renders all quads stored by using the given camera's ProjectionMatrix.
    /// </summary>
    /// <param name="camera">The required camera.</param>
    public void Render(Camera camera)
    {
        
    }
}
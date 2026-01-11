namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract;

/// <summary>
/// Represents an object whose data layout can be reflected to the graphics API.
/// </summary>
public interface IGPUReflection
{
    /// <summary>
    /// Reflects the layout of the current object to the graphics API.
    /// </summary>
    /// <param name="layout">The layout object to reflect to.</param>
    static abstract void SelfReflectToGL(VertexBufferLayout layout);
}
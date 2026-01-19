namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract;

/// <summary>
///     Represents a buffer that can be bound to the graphics context.
/// </summary>
public interface IBuffer : IBindable
{
    /// <summary>
    ///     Method that updates the buffer, be it uploading data to the GPU or changing the buffer's properties.
    /// </summary>
    public void Update();
}
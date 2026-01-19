using ThirtyDollarVisualizer.Engine.Renderer.Enums;

namespace ThirtyDollarVisualizer.Engine.Renderer.Abstract;

/// <summary>
///     Represents a bindable object.
/// </summary>
public interface IBindable
{
    /// <summary>
    ///     Gets the current state of the object's buffer state.
    ///     Indicates whether it's in pending creation, successfully created, or has encountered a failure.
    /// </summary>
    public BufferState BufferState { get; }

    /// <summary>
    ///     The handle of the object.
    /// </summary>
    public int Handle { get; }

    /// <summary>
    ///     Method that binds the object to the graphics context.
    /// </summary>
    public void Bind();
}
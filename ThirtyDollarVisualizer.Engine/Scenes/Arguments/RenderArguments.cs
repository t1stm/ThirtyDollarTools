namespace ThirtyDollarVisualizer.Engine.Scenes.Arguments;

/// <summary>
/// Arguments passed to the render call.
/// </summary>
/// <remarks>
/// Can be allocated only on the stack.
/// </remarks>
public ref struct RenderArguments
{
    /// <summary>
    /// Time elapsed since render call.
    /// </summary>
    public double Delta;
}
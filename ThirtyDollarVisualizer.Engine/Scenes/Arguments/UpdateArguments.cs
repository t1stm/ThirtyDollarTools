namespace ThirtyDollarVisualizer.Engine.Scenes.Arguments;

/// <summary>
///     Arguments passed to the update call.
/// </summary>
/// <remarks>
///     Can be allocated only on the stack.
/// </remarks>
public ref struct UpdateArguments
{
    /// <summary>
    ///     Time elapsed since last update call.
    /// </summary>
    public double Delta;
}
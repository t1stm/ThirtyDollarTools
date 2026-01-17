using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Engine.Scenes.Arguments;

/// <summary>
/// Arguments passed to the initialization call.
/// </summary>
/// <remarks>
/// Can be allocated only on the stack.
/// </remarks>
public ref struct InitArguments
{
    public Vector2i StartingResolution;
    public GLInfo GLInfo;
}
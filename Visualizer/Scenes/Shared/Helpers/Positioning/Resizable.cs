using Sundex.Engine.Renderer.Abstract;

namespace Shared.Helpers.Positioning;

public struct Resizable
{
    public IPositionable Renderable;
    public Action<float, float>? OnResize;
}
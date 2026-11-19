using Sundex.Engine.Renderer.Shaders;

namespace Sundex.Core;

/// <summary>A renderable that draws nothing: a stand-in for tests and placeholders.</summary>
public class DummyRenderable : Renderable
{
    public override Shader Shader
    {
        get => Shader.Dummy;
        set => throw new NotSupportedException("Setting the dummy shader is not allowed.");
    }
}

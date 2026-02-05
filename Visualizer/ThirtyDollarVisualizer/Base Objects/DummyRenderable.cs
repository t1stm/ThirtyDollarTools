using ThirtyDollarVisualizer.Engine.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Base_Objects;

public class DummyRenderable : Renderable
{
    public override Shader Shader
    {
        get => Shader.Dummy;
        set => throw new NotSupportedException("Setting the dummy shader is not allowed.");
    }
}
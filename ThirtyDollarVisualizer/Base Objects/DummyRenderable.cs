using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Objects;

public class DummyRenderable : Renderable
{
    public override Shader? Shader
    {
        get => Shader.Dummy;
        set => throw new NotSupportedException("Setting the dummy shader is not allowed.");
    }
}
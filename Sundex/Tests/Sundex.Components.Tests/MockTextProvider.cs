using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Components.Tests;

public class MockTextProvider : ITextProvider
{
    public IGlyphProvider GlyphProvider { get; } = new MockGlyphProvider();

    public void BindAndSetUniforms(Camera camera)
    {
    }
}

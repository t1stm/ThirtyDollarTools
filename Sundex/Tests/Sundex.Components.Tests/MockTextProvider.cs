using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Components.Tests;

public class MockTextProvider(IGlyphProvider? glyphProvider = null) : ITextProvider
{
    public IGlyphProvider GlyphProvider { get; } = glyphProvider ?? new MockGlyphProvider();

    public void BindAndSetUniforms(Camera camera)
    {
    }
}

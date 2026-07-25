using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Components.Tests;

public class MockTextProvider : ITextProvider
{
    public IGlyphProvider GlyphProvider { get; } = new MockGlyphProvider();
    public float TextureWidth => 1024;
    public float TextureHeight => 1024;

    public (Vector4, TextAlignmentData) GetTextCharacterRect(ReadOnlySpan<char> character)
    {
        return (new Vector4(0, 0, 10, 10), GlyphProvider.GetSizingData(character));
    }

    public void BindAndSetUniforms(Camera camera)
    {
    }
}
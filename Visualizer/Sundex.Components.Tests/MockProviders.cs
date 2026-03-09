using Msdfgen.Extensions;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Fonts;
using Vector2 = Msdfgen.Vector2;

namespace Sundex.Components.Tests;

public class MockFontProvider : IFontProvider
{
    public FontHandle GetFont(ReadOnlySpan<char> fontName)
    {
        return default;
    }
}

public class MockGlyphProvider : IGlyphProvider
{
    public FontHandle GetFont()
    {
        return default;
    }

    public Image<RgbaVector> GetGlyph(ReadOnlySpan<char> character)
    {
        return new Image<RgbaVector>(1, 1);
    }

    public FontMetrics GetFontMetrics()
    {
        return new FontMetrics
        {
            AscenderY = 1,
            DescenderY = -0.2,
            LineHeight = 1.2,
            UnderlineY = -0.1,
            UnderlineThickness = 0.05,
            EmSize = 1
        };
    }

    public TextAlignmentData GetSizingData(ReadOnlySpan<char> character)
    {
        return new TextAlignmentData
        {
            AdvanceInUnitSpace = 0.6,
            Scale = new Vector2(1, 1),
            Translate = new Vector2(0, 0)
        };
    }
}

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
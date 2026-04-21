using Msdfgen;
using Msdfgen.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Components.Tests;

public class MockGlyphProvider : IGlyphProvider
{
    public FontHandle GetFont()
    {
        return null!;
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
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.Engine.Text.Fonts;
using Sundex.MSDF.Fonts;

namespace Sundex.Components.Tests;

public class MockGlyphProvider : IGlyphProvider
{
    public Image<Rgb48> GetGlyph(ReadOnlySpan<char> character)
    {
        return new Image<Rgb48>(1, 1);
    }

    // Em-normalised rather than raw font units, so the numbers stay readable: TextInputTests
    // depends on LineHeight/EmSize being 1.2 and on 0.6 advance × 16 px = 9.6 px per character.
    public MsdfFontMetrics GetFontMetrics()
    {
        return new MsdfFontMetrics(
            1,
            1,
            -0.2,
            1.2,
            -0.1,
            0.05);
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

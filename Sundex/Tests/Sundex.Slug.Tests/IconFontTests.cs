using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.Components;
using Sundex.Slug.Geometry;

namespace Sundex.Slug.Tests;

/// <summary>
///     The bundled icon font (Lucide) and the icons the UI takes from it. Lucide is made from
///     stroked SVGs, so its outlines overlap themselves: the non-zero fill has to union them.
/// </summary>
public class IconFontTests(ITestOutputHelper output)
{
    public static TheoryData<string, string, float> IconsAtSizes()
    {
        var data = new TheoryData<string, string, float>();
        foreach (var (name, glyph) in Icons.All)
        foreach (var size in (float[])[16, 48])
            data.Add(name, glyph, size);
        return data;
    }

    [Fact]
    public void EveryIconIsARealGlyph()
    {
        var data = TestFonts.Shared(TestFonts.Lucide).SlugData;

        foreach (var (name, glyph) in Icons.All)
        {
            var slugGlyph = data.GetGlyph(glyph);
            Assert.True(slugGlyph != data.NotDef, $"{name} is .notdef: the codepoint isn't in the font");
            Assert.True(SlugDataTests.Decode(data, slugGlyph).CurveCount > 0, $"{name} has no curves");
        }
    }

    [Theory]
    [MemberData(nameof(IconsAtSizes))]
    public void IconMatchesTheOutline(string name, string glyph, float pixelsPerEm)
    {
        var font = TestFonts.Shared(TestFonts.Lucide);
        var data = font.SlugData;
        var shape = new Shape();
        Assert.True(font.TryBuildShape(glyph, shape, out _));

        var (mismatched, counted) = SlugCoverageTests.Compare(data, data.GetGlyph(glyph), shape, pixelsPerEm);

        var rate = mismatched / (double)counted;
        Assert.True(rate <= 0.005, $"{name}: {mismatched} of {counted} pixels ({rate:P2}) disagree with the outline");
    }

    /// <summary>
    ///     Not an assertion: logs where each icon's ink sits against Lato Bold's lines, which an
    ///     icon label needs to centre itself beside text, and writes a contact sheet for a human look.
    /// </summary>
    [Fact]
    public void LogMetricsAndWriteContactSheet()
    {
        var lucide = TestFonts.Shared(TestFonts.Lucide);
        var lato = TestFonts.Shared(TestFonts.LatoBold);
        var pad = SlugFontData.QuadPadding;

        var m = lato.Metrics;
        var xHeight = lato.SlugData.GetGlyph("x").EmRect.W - pad;
        output.WriteLine($"Lato Bold: ascender {m.AscenderY / m.EmSize:F3}, descender {m.DescenderY / m.EmSize:F3}, " +
                         $"x-height {xHeight:F3}, cap height {lato.SlugData.GetGlyph("H").EmRect.W - pad:F3} em");
        var lm = lucide.Metrics;
        output.WriteLine($"Lucide: ascender {lm.AscenderY / lm.EmSize:F3}, descender {lm.DescenderY / lm.EmSize:F3}, " +
                         $"line height {lm.LineHeight / lm.EmSize:F3} em");

        foreach (var (name, glyph) in Icons.All)
        {
            var g = lucide.SlugData.GetGlyph(glyph);
            output.WriteLine($"{name}: advance {g.Advance:F3}, ink x {g.EmRect.X + pad:F3}..{g.EmRect.Z - pad:F3}, " +
                             $"y {g.EmRect.Y + pad:F3}..{g.EmRect.W - pad:F3} em");
        }

        var path = Path.Combine(AppContext.BaseDirectory, "lucide-contact-sheet.png");
        ContactSheet(lucide.SlugData, Icons.All.Select(i => i.Glyph).ToArray(), 48).SaveAsPng(path);
        output.WriteLine($"Contact sheet: {path}");
    }

    /// <summary>Each glyph drawn at <paramref name="pixelsPerEm" /> in its own one-em cell, black on white.</summary>
    private static Image<L8> ContactSheet(SlugFontData data, string[] glyphs, int pixelsPerEm)
    {
        var cell = pixelsPerEm + 16;
        var image = new Image<L8>(cell * glyphs.Length, cell, new L8(255));

        for (var i = 0; i < glyphs.Length; i++)
        {
            var glyph = data.GetGlyph(glyphs[i]);
            var centre = new Vector2((glyph.EmRect.X + glyph.EmRect.Z) / 2, (glyph.EmRect.Y + glyph.EmRect.W) / 2);

            for (var py = 0; py < cell; py++)
            for (var px = 0; px < cell; px++)
            {
                // Y-up em space, centred on the glyph's ink box.
                var em = centre + new Vector2(px + 0.5f - cell / 2f, cell / 2f - py - 0.5f) / pixelsPerEm;

                // The GPU only samples inside the glyph's quad.
                if (em.X < glyph.EmRect.X || em.X > glyph.EmRect.Z || em.Y < glyph.EmRect.Y || em.Y > glyph.EmRect.W)
                    continue;

                var coverage = SlugReference.SlugRender(data, glyph, em, new Vector2(pixelsPerEm));
                image[i * cell + px, py] = new L8((byte)(255 - coverage * 255));
            }
        }

        return image;
    }
}

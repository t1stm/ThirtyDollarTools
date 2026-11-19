using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace Sundex.Slug.Tests;

/// <summary>
///     The placement and banding rules the Slug shader relies on without checking: things it reads
///     along a row without wrapping, the band sort order its early exit needs, and complete bands.
///     Checked over every glyph of every bundled font, straight from the texture data.
/// </summary>
public class SlugDataTests(ITestOutputHelper output)
{
    private const int Width = SlugFontData.TextureWidth;

    public static TheoryData<string> Fonts => [TestFonts.LatoRegular, TestFonts.LatoBold, TestFonts.Twemoji, TestFonts.Lucide];

    [Theory]
    [MemberData(nameof(Fonts))]
    public void HeadersListsAndCurvesNeverCrossARowEnd(string fontName)
    {
        foreach (var (label, glyph, data) in AllGlyphs(fontName))
        {
            var decoded = Decode(data, glyph);
            Assert.True(decoded.LocX + decoded.HorizontalBands + decoded.VerticalBands <= Width, $"{label}: header run crosses a row");

            foreach (var band in decoded.Bands)
            {
                Assert.True(band.Count == 0 || band.StartX + band.Count <= Width, $"{label}: band list crosses a row");
                foreach (var curve in band.Curves)
                    Assert.True(curve.X + 1 < Width, $"{label}: curve at ({curve.X}, {curve.Y}) straddles a row");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Fonts))]
    public void BandListsAreSortedFarthestFirst(string fontName)
    {
        foreach (var (label, glyph, data) in AllGlyphs(fontName))
        foreach (var band in Decode(data, glyph).Bands)
        {
            var keys = band.Curves.Select(curve => band.Vertical ? curve.MaxY : curve.MaxX).ToArray();
            for (var index = 1; index < keys.Length; index++)
                Assert.True(keys[index] <= keys[index - 1], $"{label}: band list out of order at {index}");
        }
    }

    [Theory]
    [MemberData(nameof(Fonts))]
    public void LinesNeverSitInBandsParallelToThem(string fontName)
    {
        foreach (var (label, glyph, data) in AllGlyphs(fontName))
        foreach (var band in Decode(data, glyph).Bands)
        foreach (var curve in band.Curves)
        {
            if (band.Vertical) Assert.False(curve.IsVerticalLine, $"{label}: vertical line in a vertical band");
            else Assert.False(curve.IsHorizontalLine, $"{label}: horizontal line in a horizontal band");
        }
    }

    /// <summary>
    ///     A curve missing from a band its range touches is a hole in the glyph wherever the ray
    ///     would have crossed it. Bands are rebuilt here from <see cref="SlugGlyph.Banding" />, with
    ///     the edge bands open-ended because the shader clamps band indexes.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fonts))]
    public void EveryCurveIsInEveryBandItTouches(string fontName)
    {
        // Overlaps thinner than this are float noise at a band boundary, not real contact.
        const double slack = 1e-5;
        const double epsilon = 1 / 1024.0;

        foreach (var (label, glyph, data) in AllGlyphs(fontName))
        {
            var decoded = Decode(data, glyph);
            var curves = decoded.Bands.SelectMany(band => band.Curves).Distinct().ToList();

            foreach (var curve in curves)
            foreach (var vertical in (bool[])[false, true])
            {
                if (vertical ? curve.IsVerticalLine : curve.IsHorizontalLine) continue;

                var bandCount = vertical ? decoded.VerticalBands : decoded.HorizontalBands;
                double scale = vertical ? glyph.Banding.X : glyph.Banding.Y;
                double offset = vertical ? glyph.Banding.Z : glyph.Banding.W;
                double low = (vertical ? curve.MinX : curve.MinY) - epsilon;
                double high = (vertical ? curve.MaxX : curve.MaxY) + epsilon;

                for (var index = 0; index < bandCount; index++)
                {
                    var bandLo = index == 0 || scale == 0 ? double.NegativeInfinity : (index - offset) / scale;
                    var bandHi = index == bandCount - 1 || scale == 0 ? double.PositiveInfinity : (index + 1 - offset) / scale;
                    if (!(low < bandHi - slack && high > bandLo + slack)) continue;

                    var band = decoded.Bands.Single(candidate => candidate.Vertical == vertical && candidate.Index == index);
                    Assert.True(band.Curves.Contains(curve),
                        $"{label}: curve ({curve.X}, {curve.Y}) missing from {(vertical ? "vertical" : "horizontal")} band {index}");
                }
            }
        }
    }

    /// <summary>
    ///     The band-count search runs its helpers <c>Vector&lt;T&gt;</c>-wide. They must agree with the
    ///     scalar maths <c>TryGetBandRange</c> uses at every length, including the scalar tail.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fonts))]
    public void GlyphTableRowHoldsTheGlyphsShaderData(string fontName)
    {
        foreach (var (label, glyph, data) in AllGlyphs(fontName))
        {
            Assert.True(glyph.EmRect == GlyphTexel(data, glyph, 0), $"{label}: em rect differs from its table row");
            Assert.True(glyph.Banding == GlyphTexel(data, glyph, 1), $"{label}: banding differs from its table row");
            Assert.True(glyph.GlyphData == GlyphTexel(data, glyph, 2), $"{label}: glyph data differs from its table row");
        }
    }

    [Fact]
    public void BandHelpersMatchScalarMaths()
    {
        var random = new Random(7);
        for (var length = 0; length < 23; length++)
        {
            var low = Enumerable.Range(0, length).Select(_ => random.NextDouble() * 1.2 - 0.1).ToArray();
            var high = low.Select(start => start + random.NextDouble() * 0.5).ToArray();
            var first = new double[length];
            var last = new double[length];

            for (var bandCount = 1; bandCount <= SlugFontData.MaxBands; bandCount++)
            {
                double scale = bandCount;
                SlugFontData.Builder.BandIndices(low, scale, bandCount, first);
                SlugFontData.Builder.BandIndices(high, scale, bandCount, last);

                for (var index = 0; index < length; index++)
                {
                    Assert.Equal(Math.Clamp((int)Math.Floor(low[index] * scale), 0, bandCount - 1), first[index]);
                    Assert.Equal(Math.Clamp((int)Math.Floor(high[index] * scale), 0, bandCount - 1), last[index]);
                }

                for (var band = 0; band < bandCount; band++)
                    Assert.Equal(Enumerable.Range(0, length).Count(index => first[index] <= band && band <= last[index]),
                        SlugFontData.Builder.CountInBand(first, last, band));
            }
        }
    }

    [Fact]
    public void BuildCostOfTheBundledFonts()
    {
        foreach (var fontName in (string[])[TestFonts.LatoRegular, TestFonts.LatoBold, TestFonts.Twemoji, TestFonts.Lucide])
        {
            using var stream = TestFonts.Open(fontName);
            var font = SlugFont.Load(stream);

            var stopwatch = Stopwatch.StartNew();
            var data = font.SlugData;
            stopwatch.Stop();

            output.WriteLine($"{fontName}: {data.GlyphCount} glyphs, {data.CurveRows} curve rows, " +
                             $"{data.BandRows} band rows, {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
        }
    }

    private static IEnumerable<(string Label, SlugGlyph Glyph, SlugFontData Data)> AllGlyphs(string fontName)
    {
        var font = TestFonts.Shared(fontName);
        var data = font.SlugData;

        yield return ($"{fontName} .notdef", data.NotDef, data);

        foreach (var codePoint in font.GetAvailableCodePoints().ToArray())
        {
            if (!Rune.TryCreate(codePoint.Value, out var rune)) continue;
            yield return ($"{fontName} U+{rune.Value:X4}", data.GetGlyph(rune.ToString()), data);
        }
    }

    /// <summary>Texel <paramref name="texel" /> of <paramref name="glyph" />'s row, read the way Batched.vert reads it.</summary>
    private static Vector4 GlyphTexel(SlugFontData data, SlugGlyph glyph, int texel)
    {
        var index = glyph.Index * SlugFontData.TexelsPerGlyph + texel;
        var start = ((index >> 12) * Width + (index & (Width - 1))) * 4;
        return new Vector4(data.Glyphs[start], data.Glyphs[start + 1], data.Glyphs[start + 2], data.Glyphs[start + 3]);
    }

    internal record struct DecodedCurve(int X, int Y, Vector2 Point1, Vector2 Point2, Vector2 Point3)
    {
        public float MinX => MathF.Min(MathF.Min(Point1.X, Point2.X), Point3.X);
        public float MaxX => MathF.Max(MathF.Max(Point1.X, Point2.X), Point3.X);
        public float MinY => MathF.Min(MathF.Min(Point1.Y, Point2.Y), Point3.Y);
        public float MaxY => MathF.Max(MathF.Max(Point1.Y, Point2.Y), Point3.Y);
        public bool IsHorizontalLine => Point1.Y == Point2.Y && Point2.Y == Point3.Y;
        public bool IsVerticalLine => Point1.X == Point2.X && Point2.X == Point3.X;
    }

    internal record DecodedBand(bool Vertical, int Index, int StartX, int Count, List<DecodedCurve> Curves);

    internal record DecodedGlyph(int LocX, int LocY, int HorizontalBands, int VerticalBands, List<DecodedBand> Bands)
    {
        public int CurveCount => Bands.SelectMany(band => band.Curves).Distinct().Count();
    }

    /// <summary>Reads a glyph's bands and curves back out of the textures, the way the shader addresses them.</summary>
    internal static DecodedGlyph Decode(SlugFontData data, SlugGlyph glyph)
    {
        int locX = (int)glyph.GlyphData.X, locY = (int)glyph.GlyphData.Y;
        int verticalBands = (int)glyph.GlyphData.Z + 1, horizontalBands = (int)glyph.GlyphData.W + 1;

        var bands = new List<DecodedBand>();
        for (var band = 0; band < horizontalBands + verticalBands; band++)
        {
            var header = Texel(data.Bands, locX + band, locY);
            var count = (int)header.X;
            var linear = locY * Width + locX + (int)header.Y;
            int startX = linear % Width, startY = linear / Width;

            var curves = new List<DecodedCurve>();
            for (var index = 0; index < count && startX + index < Width; index++)
            {
                var entry = Texel(data.Bands, startX + index, startY);
                int x = (int)entry.X, y = (int)entry.Y;
                var first = Texel(data.Curves, x, y);
                var second = x + 1 < Width ? Texel(data.Curves, x + 1, y) : Vector4.Zero;

                curves.Add(new DecodedCurve(x, y, new Vector2(first.X, first.Y), new Vector2(first.Z, first.W),
                    new Vector2(second.X, second.Y)));
            }

            var vertical = band >= horizontalBands;
            bands.Add(new DecodedBand(vertical, vertical ? band - horizontalBands : band, startX, count, curves));
        }

        return new DecodedGlyph(locX, locY, horizontalBands, verticalBands, bands);
    }

    private static Vector4 Texel(float[] texture, int x, int y)
    {
        var offset = (y * Width + x) * 4;
        return new Vector4(texture[offset], texture[offset + 1], texture[offset + 2], texture[offset + 3]);
    }
}

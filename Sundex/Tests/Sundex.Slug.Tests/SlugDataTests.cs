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
            Assert.True(decoded.LocX + decoded.Nh + decoded.Nv <= Width, $"{label}: header run crosses a row");

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
            var keys = band.Curves.Select(c => band.Vertical ? c.MaxY : c.MaxX).ToArray();
            for (var i = 1; i < keys.Length; i++)
                Assert.True(keys[i] <= keys[i - 1], $"{label}: band list out of order at {i}");
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
            var curves = decoded.Bands.SelectMany(b => b.Curves).Distinct().ToList();

            foreach (var curve in curves)
            foreach (var vertical in (bool[])[false, true])
            {
                if (vertical ? curve.IsVerticalLine : curve.IsHorizontalLine) continue;

                var n = vertical ? decoded.Nv : decoded.Nh;
                double scale = vertical ? glyph.Banding.X : glyph.Banding.Y;
                double offset = vertical ? glyph.Banding.Z : glyph.Banding.W;
                double lo = (vertical ? curve.MinX : curve.MinY) - epsilon;
                double hi = (vertical ? curve.MaxX : curve.MaxY) + epsilon;

                for (var i = 0; i < n; i++)
                {
                    var bandLo = i == 0 || scale == 0 ? double.NegativeInfinity : (i - offset) / scale;
                    var bandHi = i == n - 1 || scale == 0 ? double.PositiveInfinity : (i + 1 - offset) / scale;
                    if (!(lo < bandHi - slack && hi > bandLo + slack)) continue;

                    var band = decoded.Bands.Single(b => b.Vertical == vertical && b.Index == i);
                    Assert.True(band.Curves.Contains(curve),
                        $"{label}: curve ({curve.X}, {curve.Y}) missing from {(vertical ? "vertical" : "horizontal")} band {i}");
                }
            }
        }
    }

    /// <summary>
    ///     The band-count search runs its helpers <c>Vector&lt;T&gt;</c>-wide. They must agree with the
    ///     scalar maths <c>TryGetBandRange</c> uses at every length, including the scalar tail.
    /// </summary>
    [Fact]
    public void BandHelpersMatchScalarMaths()
    {
        var random = new Random(7);
        for (var length = 0; length < 23; length++)
        {
            var lo = Enumerable.Range(0, length).Select(_ => random.NextDouble() * 1.2 - 0.1).ToArray();
            var hi = lo.Select(v => v + random.NextDouble() * 0.5).ToArray();
            var first = new double[length];
            var last = new double[length];

            for (var n = 1; n <= SlugFontData.MaxBands; n++)
            {
                double scale = n;
                SlugFontData.Builder.BandIndices(lo, scale, n, first);
                SlugFontData.Builder.BandIndices(hi, scale, n, last);

                for (var i = 0; i < length; i++)
                {
                    Assert.Equal(Math.Clamp((int)Math.Floor(lo[i] * scale), 0, n - 1), first[i]);
                    Assert.Equal(Math.Clamp((int)Math.Floor(hi[i] * scale), 0, n - 1), last[i]);
                }

                for (var band = 0; band < n; band++)
                    Assert.Equal(Enumerable.Range(0, length).Count(i => first[i] <= band && band <= last[i]),
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

    internal record struct DecodedCurve(int X, int Y, Vector2 P1, Vector2 P2, Vector2 P3)
    {
        public float MinX => MathF.Min(MathF.Min(P1.X, P2.X), P3.X);
        public float MaxX => MathF.Max(MathF.Max(P1.X, P2.X), P3.X);
        public float MinY => MathF.Min(MathF.Min(P1.Y, P2.Y), P3.Y);
        public float MaxY => MathF.Max(MathF.Max(P1.Y, P2.Y), P3.Y);
        public bool IsHorizontalLine => P1.Y == P2.Y && P2.Y == P3.Y;
        public bool IsVerticalLine => P1.X == P2.X && P2.X == P3.X;
    }

    internal record DecodedBand(bool Vertical, int Index, int StartX, int Count, List<DecodedCurve> Curves);

    internal record DecodedGlyph(int LocX, int LocY, int Nh, int Nv, List<DecodedBand> Bands)
    {
        public int CurveCount => Bands.SelectMany(b => b.Curves).Distinct().Count();
    }

    /// <summary>Reads a glyph's bands and curves back out of the textures, the way the shader addresses them.</summary>
    internal static DecodedGlyph Decode(SlugFontData data, SlugGlyph glyph)
    {
        int locX = (int)glyph.GlyphData.X, locY = (int)glyph.GlyphData.Y;
        int nv = (int)glyph.GlyphData.Z + 1, nh = (int)glyph.GlyphData.W + 1;

        var bands = new List<DecodedBand>();
        for (var h = 0; h < nh + nv; h++)
        {
            var header = Texel(data.Bands, locX + h, locY);
            var count = (int)header.X;
            var linear = locY * Width + locX + (int)header.Y;
            int startX = linear % Width, startY = linear / Width;

            var curves = new List<DecodedCurve>();
            for (var i = 0; i < count && startX + i < Width; i++)
            {
                var entry = Texel(data.Bands, startX + i, startY);
                int x = (int)entry.X, y = (int)entry.Y;
                var first = Texel(data.Curves, x, y);
                var second = x + 1 < Width ? Texel(data.Curves, x + 1, y) : Vector4.Zero;

                curves.Add(new DecodedCurve(x, y, new Vector2(first.X, first.Y), new Vector2(first.Z, first.W),
                    new Vector2(second.X, second.Y)));
            }

            var vertical = h >= nh;
            bands.Add(new DecodedBand(vertical, vertical ? h - nh : h, startX, count, curves));
        }

        return new DecodedGlyph(locX, locY, nh, nv, bands);
    }

    private static Vector4 Texel(float[] texture, int x, int y)
    {
        var i = (y * Width + x) * 4;
        return new Vector4(texture[i], texture[i + 1], texture[i + 2], texture[i + 3]);
    }
}

using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Tests;

/// <summary>
///     Pins the space glyph outlines are built in: em-normalised, Y-up, sitting on the baseline.
///     Those are the coordinates the engine lays text out with, so a change here moves every
///     glyph on screen.
/// </summary>
public class CoordinateContractTests
{
    /// <summary>
    ///     Pins the direction of the Y flip: descenders must reach clearly below the baseline,
    ///     and glyphs that sit on it must not. The thresholds straddle Lato's period, which
    ///     genuinely overshoots the baseline by 15 font units (0.0075 em) — real, and nothing
    ///     like a flipped axis.
    /// </summary>
    [Theory]
    [InlineData('g', true)]
    [InlineData('j', true)]
    [InlineData('_', true)]
    [InlineData('A', false)]
    [InlineData('.', false)]
    public void DescendersSitBelowTheBaseline(char character, bool expectedBelow)
    {
        using var stream = TestFonts.Open(TestFonts.LatoRegular);
        var font = MsdfFont.Load(stream);

        var shape = new Shape();
        Assert.True(font.TryBuildShape([character], shape, out _));

        var bounds = shape.GetBounds();
        Assert.Equal(expectedBelow, bounds.B < -0.05);
    }

    /// <summary>The underscore is the one glyph here that lives entirely under the baseline.</summary>
    [Fact]
    public void UnderscoreIsEntirelyBelowTheBaseline()
    {
        using var stream = TestFonts.Open(TestFonts.LatoRegular);
        var font = MsdfFont.Load(stream);

        var shape = new Shape();
        Assert.True(font.TryBuildShape("_", shape, out _));

        Assert.True(shape.GetBounds().T < 0, "underscore should not reach the baseline");
    }

    /// <summary>
    ///     Catches the classic double-reverse: the Y flip inverts winding, so a solid glyph's
    ///     single contour must come out with one consistent orientation, not two competing fixes.
    /// </summary>
    [Fact]
    public void SolidGlyphHasExactlyOneContourWithConsistentWinding()
    {
        using var stream = TestFonts.Open(TestFonts.LatoRegular);
        var font = MsdfFont.Load(stream);

        var shape = new Shape();
        Assert.True(font.TryBuildShape(".", shape, out _));

        Assert.Equal(1, shape.ContourCount);
        Assert.True(shape.Validate(), "contour must be a closed chain");
        Assert.NotEqual(0, Math.Sign(shape.ContourSignedArea(0)));
    }

    /// <summary>A glyph with a hole must produce two contours wound against each other.</summary>
    [Fact]
    public void HoledGlyphHasOppositelyWoundContours()
    {
        using var stream = TestFonts.Open(TestFonts.LatoRegular);
        var font = MsdfFont.Load(stream);

        var shape = new Shape();
        Assert.True(font.TryBuildShape("o", shape, out _));

        Assert.Equal(2, shape.ContourCount);
        Assert.True(shape.Validate());
        Assert.NotEqual(
            Math.Sign(shape.ContourSignedArea(0)),
            Math.Sign(shape.ContourSignedArea(1)));
    }

    [Theory]
    [InlineData(TestFonts.LatoRegular, 2000, 1974, -426, 2400)]
    [InlineData(TestFonts.LatoBold, 2000, 1974, -426, 2400)]
    [InlineData(TestFonts.Twemoji, 512, 448, -64, 558)]
    public void MetricsMatchTheFontsOwnTables(string font, double em, double ascender, double descender,
        double lineHeight)
    {
        using var stream = TestFonts.Open(font);
        var metrics = MsdfFont.Load(stream).Metrics;

        Assert.Equal(em, metrics.EmSize);
        Assert.Equal(ascender, metrics.AscenderY);
        Assert.Equal(descender, metrics.DescenderY);
        Assert.Equal(lineHeight, metrics.LineHeight);

        // The only thing FlexLinePositioningProvider actually reads.
        Assert.Equal(lineHeight / em, metrics.LineHeight / metrics.EmSize, 9);
    }
}

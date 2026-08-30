namespace Sundex.MSDF.Tests;

/// <summary>
///     A glyph that exists but carries no ink — space, above all — must still generate.
/// </summary>
public class BlankGlyphTests
{
    private const int GlyphSize = 48;
    private const double PxRange = 4.0;
    private const int Channels = 3;

    [Theory]
    [InlineData(' ')]
    [InlineData('\u00A0')] // no-break space
    public void BlankGlyphGeneratesWithItsAdvance(char character)
    {
        using var stream = TestFonts.Open(TestFonts.LatoRegular);
        var font = MsdfFont.Load(stream);

        var buffer = new float[GlyphSize * GlyphSize * Channels];
        Assert.True(font.TryGenerate([character], buffer, GlyphSize, Channels, PxRange, out var glyph));

        Assert.True(glyph.Advance > 0, "a blank glyph still advances the pen");

        // Every pixel reads as outside the shape, which is what the shader's median test needs.
        for (var i = 0; i < buffer.Length; i += Channels)
        {
            var median = Math.Max(Math.Min(buffer[i], buffer[i + 1]), Math.Min(Math.Max(buffer[i], buffer[i + 1]), buffer[i + 2]));
            Assert.False(float.IsNaN(median));
            Assert.True(median < 0.5f);
        }
    }

    /// <summary>
    ///     False is reserved for input that decodes to nothing; a codepoint the font does not
    ///     cover comes back as its <c>.notdef</c> box instead.
    /// </summary>
    [Fact]
    public void EmptyInputReturnsFalse()
    {
        using var stream = TestFonts.Open(TestFonts.LatoRegular);
        var font = MsdfFont.Load(stream);

        var buffer = new float[GlyphSize * GlyphSize * Channels];
        Assert.False(font.TryGenerate([], buffer, GlyphSize, Channels, PxRange, out _));
    }
}

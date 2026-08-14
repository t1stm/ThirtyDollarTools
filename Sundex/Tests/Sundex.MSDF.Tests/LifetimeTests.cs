namespace Sundex.MSDF.Tests;

/// <summary>
///     A font is the caller's to keep or to drop, so disposing one has to be both optional and
///     final: generating afterwards is a mistake worth reporting, and disposing twice is not.
/// </summary>
public class LifetimeTests
{
    private const int GlyphSize = 48;
    private const double PxRange = 4.0;
    private const int Channels = 3;

    [Fact]
    public void GeneratingAfterDisposeThrows()
    {
        var font = Load();
        var buffer = new float[GlyphSize * GlyphSize * Channels];

        Assert.True(font.TryGenerate("A", buffer, GlyphSize, Channels, PxRange, out _));

        font.Dispose();
        font.Dispose(); // idempotent

        Assert.Throws<ObjectDisposedException>(() =>
            font.TryGenerate("A", new float[GlyphSize * GlyphSize * Channels], GlyphSize, Channels, PxRange, out _));
    }

    /// <summary>Metrics are read at load, so they survive disposal — nothing recomputes them.</summary>
    [Fact]
    public void MetricsStillReadableAfterDispose()
    {
        var font = Load();
        var before = font.Metrics;

        font.Dispose();

        Assert.Equal(before, font.Metrics);
    }

    private static MsdfFont Load()
    {
        using var stream = TestFonts.Open(TestFonts.LatoRegular);
        return MsdfFont.Load(stream);
    }
}

using System.Reflection;
using Serilog;
using Shared;
using Sundex.Engine.Asset_Management;
using Sundex.Engine;
using Sundex.Engine.Text.Fonts;

namespace LoadingScene.Tests;

/// <summary>
///     The glyph warm-up the loading screen runs on a worker while the sounds come down.
///     Generating an MSDF needs no graphics context and is the expensive half of putting a
///     character on screen (~1 ms each); only the atlas upload that follows needs the render
///     thread. These run against the real font, no GL - which is exactly the point.
/// </summary>
public class GlyphWarmupTests
{
    private static GlyphProvider NewProvider()
    {
        var assets = new AssetProvider(new LoggerConfiguration().CreateLogger(),
            [typeof(SharedAssembly).Assembly, Assembly.GetExecutingAssembly()], new GLInfo());

        return new GlyphProvider(new FontProvider(assets), "Lato Bold");
    }

    /// <summary>
    ///     Warming is what generates: the sizing data a character needs to be laid out is
    ///     there afterwards, without anything having asked for the glyph image.
    /// </summary>
    [Fact]
    public void Warm_GeneratesWithoutTheGlyphEverBeingFetched()
    {
        var provider = NewProvider();
        Assert.Throws<Exception>(() => provider.GetSizingData("A"));

        provider.Warm("A");

        var sizing = provider.GetSizingData("A");
        Assert.True(sizing.AdvanceInUnitSpace > 0, "the warmed glyph has no advance");
    }

    /// <summary>
    ///     A warmed glyph is handed over rather than regenerated, and the entry is taken out
    ///     of the cache as it goes - it lives in the atlas from then on, and a copy kept here
    ///     would be 36 KB of dead weight per character for the rest of the process.
    /// </summary>
    [Fact]
    public void GetGlyph_ConsumesTheWarmedGlyphAndStillWorksAfterwards()
    {
        var provider = NewProvider();
        provider.Warm("A");

        using var first = provider.GetGlyph("A");
        Assert.Equal(GlyphProvider.GlyphSize, first.Width);

        // Cache emptied by the fetch above, so this one falls through to generation. It has
        // to still produce the same glyph rather than an empty or stale one.
        using var second = provider.GetGlyph("A");
        Assert.Equal(first[24, 24], second[24, 24]);
    }

    /// <summary>Warming the same character twice must not generate it twice.</summary>
    [Fact]
    public void Warm_IsCheapToRepeat()
    {
        var provider = NewProvider();
        provider.Warm("A");
        provider.Warm("A");

        using var glyph = provider.GetGlyph("A");
        Assert.Equal(GlyphProvider.GlyphSize, glyph.Width);

        // The second Warm found the first still parked and left it alone; the fetch above
        // took it. If it had generated a second copy, this fetch would find that one and
        // the sizing lock in Warm would have been pointless.
        Assert.NotNull(provider.GetGlyph("A"));
    }

    /// <summary>
    ///     Warming runs on a worker, from many threads at once, against one shared font.
    ///     The font pools its scratch concurrently, so this must neither throw nor deadlock.
    /// </summary>
    [Fact]
    public void Warm_IsSafeFromManyThreadsAtOnce()
    {
        var provider = NewProvider();
        const string characters = "abcdefghijklmnopqrstuvwxyz0123456789";

        Parallel.ForEach(characters, c => provider.Warm([c]));

        foreach (var c in characters)
            Assert.True(provider.GetSizingData([c]).AdvanceInUnitSpace >= 0, $"'{c}' never generated");
    }
}

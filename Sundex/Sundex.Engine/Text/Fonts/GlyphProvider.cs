using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.MSDF.Fonts;

namespace Sundex.Engine.Text.Fonts;

public class GlyphProvider(IFontProvider fontProvider, string fontName) : IGlyphProvider
{
    public const int GlyphSize = 48;
    public const float MsdfRange = 4.0f;

    /// <summary>An MSDF is three channels; the alpha a glyph is drawn with comes from the vertex colour.</summary>
    private const int Channels = 3;

    /// <summary>
    ///     Glyphs generated ahead of time by <see cref="Warm" />, waiting for the render
    ///     thread to come and upload them. Entries are taken out as they are consumed: an
    ///     uploaded glyph lives in the atlas from then on and this copy is dead weight.
    /// </summary>
    private readonly ConcurrentDictionary<string, Image<Rgb48>> _warmed = new();

    private MsdfFontMetrics? _cachedMetrics;

    protected Dictionary<string, TextAlignmentData> SizingData { get; } = new();

    public Image<Rgb48> GetGlyph(ReadOnlySpan<char> character)
    {
        // Warmed ahead of this frame, off the render thread - all that is left here is
        // handing it to the caller to upload.
        return _warmed.TryRemove(character.ToString(), out var warmed) ? warmed : Generate(character);
    }

    /// <inheritdoc />
    public void Warm(ReadOnlySpan<char> character)
    {
        var key = character.ToString();
        // ContainsKey rather than GetOrAdd: GetOrAdd's factory would generate the glyph
        // before the lookup could report that one is already waiting.
        if (_warmed.ContainsKey(key)) return;

        lock (SizingData)
        {
            // Already rasterised once, so it is in the atlas and will never be asked for again.
            if (SizingData.ContainsKey(key)) return;
        }

        _warmed.TryAdd(key, Generate(character));
    }

    private Image<Rgb48> Generate(ReadOnlySpan<char> character)
    {
        // The generator writes floats, the atlas stores 16-bit channels. The distances only
        // ever occupy a narrow band around 0.5, so the extra bits are what keep an antialiased
        // edge from collapsing to a handful of steps.
        var distances = new float[GlyphSize * GlyphSize * Channels];

        var font = fontProvider.GetFont(fontName);
        if (!font.TryGenerate(character, distances, GlyphSize, Channels, MsdfRange, out var glyph))
            throw new Exception($"No outline for character: {character}");

        var pixels = new Rgb48[GlyphSize * GlyphSize];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = new Rgb48(ToChannel(distances[i * Channels]),
                ToChannel(distances[i * Channels + 1]),
                ToChannel(distances[i * Channels + 2]));

        lock (SizingData)
        {
            var lookup = SizingData.GetAlternateLookup<ReadOnlySpan<char>>();
            lookup.TryAdd(character, new TextAlignmentData
            {
                AdvanceInUnitSpace = glyph.Advance,
                Scale = glyph.Scale,
                Translate = glyph.Translate
            });
        }

        return Image.WrapMemory<Rgb48>(Configuration.Default, pixels, GlyphSize, GlyphSize);
    }

    /// <summary>
    ///     Quantises a signed distance to a 16-bit channel, clamping rather than casting:
    ///     a distance runs well past the shape on both sides, and anything beyond the range
    ///     saturates to solid inside or outside.
    /// </summary>
    private static ushort ToChannel(float distance) =>
        (ushort)Math.Clamp(distance * ushort.MaxValue + 0.5f, 0f, ushort.MaxValue);

    public MsdfFontMetrics GetFontMetrics() =>
        _cachedMetrics ??= fontProvider.GetFont(fontName).Metrics;

    public TextAlignmentData GetSizingData(ReadOnlySpan<char> character)
    {
        lock (SizingData)
        {
            var lookup = SizingData.GetAlternateLookup<ReadOnlySpan<char>>();
            return lookup.TryGetValue(character, out var data)
                ? data
                : throw new Exception($"Unable to find sizing data for character: {character}");
        }
    }
}

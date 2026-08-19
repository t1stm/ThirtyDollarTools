using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sundex.MSDF.Fonts;

namespace Sundex.Engine.Text.Fonts;

public class GlyphProvider(IFontProvider fontProvider, string fontName) : IGlyphProvider
{
    public const int GlyphSize = 48;
    public const float MsdfRange = 4.0f;

    /// <summary>RGBA — the shader reads RGB, and the alpha channel is left at 1 for the atlas format.</summary>
    private const int Channels = 4;

    /// <summary>
    ///     Glyphs generated ahead of time by <see cref="Warm" />, waiting for the render
    ///     thread to come and upload them. Entries are taken out as they are consumed: an
    ///     uploaded glyph lives in the atlas from then on and this copy is dead weight.
    /// </summary>
    private readonly ConcurrentDictionary<string, Image<RgbaVector>> _warmed = new();

    private MsdfFontMetrics? _cachedMetrics;

    protected Dictionary<string, TextAlignmentData> SizingData { get; } = new();

    public Image<RgbaVector> GetGlyph(ReadOnlySpan<char> character)
    {
        // Warmed ahead of this frame, off the render thread - all that is left here is
        // handing it to the caller to upload.
        return _warmed.TryRemove(character.ToString(), out var warmed) ? warmed : Generate(character);
    }

    /// <inheritdoc />
    public void Warm(ReadOnlySpan<char> character)
    {
        var key = character.ToString();
        // ContainsKey rather than GetOrAdd: the point is to not generate, and GetOrAdd's
        // factory would run before the lookup could tell us it was pointless.
        if (_warmed.ContainsKey(key)) return;

        lock (SizingData)
        {
            // Already rasterised once, so it is in the atlas and will never be asked for again.
            if (SizingData.ContainsKey(key)) return;
        }

        _warmed.TryAdd(key, Generate(character));
    }

    private Image<RgbaVector> Generate(ReadOnlySpan<char> character)
    {
        var pixels = new RgbaVector[GlyphSize * GlyphSize];
        var floats = MemoryMarshal.Cast<RgbaVector, float>(pixels.AsSpan());
        floats.Fill(1f); // leaves alpha at 1 where TryGenerate only writes RGB

        var font = fontProvider.GetFont(fontName);
        if (!font.TryGenerate(character, floats, GlyphSize, Channels, MsdfRange, out var glyph))
            throw new Exception($"No outline for character: {character}");

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

        return Image.WrapMemory<RgbaVector>(Configuration.Default, pixels, GlyphSize, GlyphSize);
    }

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

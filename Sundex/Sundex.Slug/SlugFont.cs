using System.Buffers;
using System.Numerics;
using System.Text;
using SixLabors.Fonts;
using SixLabors.Fonts.Rendering;
using SixLabors.Fonts.Unicode;
using Sundex.Slug.Fonts;
using Sundex.Slug.Geometry;
using FontMetrics = SixLabors.Fonts.FontMetrics;

namespace Sundex.Slug;

/// <summary>
///     A parsed font: its metrics, its glyph outlines, and the Slug data built from them. Safe to
///     share across threads.
///     <para>
///         Parsing happens once, in <see cref="Load(Stream)" />: hold an <see cref="SlugFont" />
///         for as long as its glyphs are wanted, since re-loading re-parses the file and rebuilds
///         <see cref="SlugData" />. Nothing unmanaged is held, so dropping the last reference
///         releases all of it.
///     </para>
/// </summary>
public sealed class SlugFont
{
    /// <summary>
    ///     Render DPI at which one SixLabors output unit equals one font unit when the font size
    ///     is <c>UnitsPerEm</c> (SixLabors: px = size × dpi / 72); the glyph renderer divides that
    ///     down to ems.
    /// </summary>
    private const float RenderDpi = 72f;
    
    private readonly Font _font;
    private readonly FontMetrics _fontMetrics;

    private readonly Lazy<SlugFontData> _slugData;

    private SlugFont(FontFamily family)
    {
        // Size is chosen after metrics are known, so create a probe font just to read UnitsPerEm.
        var unitsPerEm = family.CreateFont(16).FontMetrics.UnitsPerEm;
        _font = family.CreateFont(unitsPerEm);
        _fontMetrics = _font.FontMetrics;

        Metrics = new SlugFontMetrics(
            _fontMetrics.UnitsPerEm,
            _fontMetrics.HorizontalMetrics.Ascender,
            _fontMetrics.HorizontalMetrics.Descender,
            _fontMetrics.HorizontalMetrics.LineHeight,
            _fontMetrics.UnderlinePosition,
            _fontMetrics.UnderlineThickness);

        _slugData = new Lazy<SlugFontData>(() => SlugFontData.Build(this));
    }

    /// <summary>Font-wide vertical metrics, in raw font units.</summary>
    public SlugFontMetrics Metrics { get; }

    /// <summary>
    ///     The font's Slug curve and band data, built in full on first access and shared by every
    ///     caller from then on. Thread-safe.
    /// </summary>
    public SlugFontData SlugData => _slugData.Value;

    /// <summary>Parses a font from a TrueType or OpenType stream. The stream is read in full here.</summary>
    public static SlugFont Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var collection = new FontCollection();
        return new SlugFont(collection.Add(stream));
    }

    /// <summary>Parses a font from the bytes of a TrueType or OpenType file.</summary>
    public static SlugFont Load(ReadOnlySpan<byte> ttf)
    {
        using var stream = new MemoryStream([.. ttf], false);
        return Load(stream);
    }

    /// <summary>
    ///     Builds the glyph outline for <paramref name="characters" /> into
    ///     <paramref name="shape" />, in em space (Y-up, baseline origin).
    /// </summary>
    /// <returns>False when the font has no glyph for the input at all.</returns>
    internal bool TryBuildShape(ReadOnlySpan<char> characters, Shape shape, out double advance)
    {
        shape.Clear();
        advance = 0;

        if (!TryFirstCodePoint(characters, out var codePoint, out var text)) return false;

        // ColorFontSupport.None asks for the base outline. An emoji font's colour layers are a
        // stack of filled shapes, one per palette colour, which a single glyph can't carry.
        if (!_fontMetrics.TryGetGlyphMetrics(codePoint, TextAttributes.None, TextDecorations.None,
                LayoutMode.HorizontalTopBottom, ColorFontSupport.None, null, out var glyphMetrics))
            return false;

        advance = glyphMetrics.AdvanceWidth / (double)_fontMetrics.UnitsPerEm;

        var renderer = new OutlineGlyphRenderer(shape, _fontMetrics.UnitsPerEm);
        var options = new TextOptions(_font)
        {
            Dpi = RenderDpi,
            Origin = Vector2.Zero,
            ColorFontSupport = ColorFontSupport.None
        };

        TextRenderer.RenderTo(renderer, text, options);

        // A glyph with metrics but no ink — space, above all — is not a failure: it still has to
        // advance the pen. Nothing to anchor, though, since an empty shape's bounds are still the
        // sentinels.
        if (shape.EdgeCount == 0) return true;

        AnchorToBaseline(shape, glyphMetrics);
        return true;
    }

    /// <summary>Every codepoint the font maps to a glyph.</summary>
    internal ReadOnlyMemory<CodePoint> GetAvailableCodePoints() => _fontMetrics.GetAvailableCodePoints();

    /// <summary>
    ///     Moves the rendered outline onto the baseline, so the top of its ink sits at the height
    ///     the font's own metrics give it: <c>Ascender − TopSideBearing</c>. The height comes from
    ///     those metrics rather than from where SixLabors placed the outline, because SixLabors
    ///     grows its line box for ink above the ascender and so pushes tall glyphs down by their
    ///     own overshoot; both sides of the correction use this library's own curve bounds.
    /// </summary>
    private void AnchorToBaseline(Shape shape, FontGlyphMetrics glyphMetrics)
    {
        var yMax = (_fontMetrics.VerticalMetrics.Ascender - glyphMetrics.TopSideBearing)
                   / (double)_fontMetrics.UnitsPerEm;

        shape.TranslateY(yMax - shape.GetBounds().T);
    }

    /// <summary>
    ///     Decodes the first character into a codepoint, so a surrogate pair resolves to the one
    ///     scalar value it encodes, and returns the span that codepoint occupies.
    /// </summary>
    internal static bool TryFirstCodePoint(ReadOnlySpan<char> characters, out CodePoint codePoint,
        out ReadOnlySpan<char> text)
    {
        codePoint = default;
        text = default;

        // Callers pass a fixed two-char buffer for surrogate pairs, NUL-padded when the character
        // needs only one.
        var end = characters.IndexOf('\0');
        if (end >= 0) characters = characters[..end];
        if (characters.IsEmpty) return false;

        if (Rune.DecodeFromUtf16(characters, out var rune, out var consumed) != OperationStatus.Done)
            return false;

        codePoint = new CodePoint(rune.Value);
        text = characters[..consumed];
        return true;
    }
}

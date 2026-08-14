using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using SixLabors.Fonts;
using SixLabors.Fonts.Rendering;
using SixLabors.Fonts.Unicode;
using Sundex.MSDF.Coloring;
using Sundex.MSDF.Distance;
using Sundex.MSDF.Fonts;
using Sundex.MSDF.Generation;
using Sundex.MSDF.Geometry;
using FontMetrics = SixLabors.Fonts.FontMetrics;

namespace Sundex.MSDF;

/// <summary>
///     A parsed font that generates MSDF glyph bitmaps.
///     <para>
///         Parsing happens once, in <see cref="Load(Stream)" />, and the caller owns what comes
///         back: hold an <see cref="MsdfFont" /> for as long as glyphs are still wanted from it,
///         since re-loading re-parses the file. It is safe to share across threads.
///     </para>
///     <para>
///         The library keeps nothing of its own between calls. Every buffer a font ever allocated
///         hangs off that font, so dropping the last reference to it releases all of it — there is
///         no process-wide cache to grow.
///     </para>
///     <para>
///         <see cref="Dispose" /> exists for callers that want the reusable scratch released at a
///         point they choose — a tool that renders one atlas and moves on can write
///         <c>using var font = MsdfFont.Load(…)</c>. It is optional: nothing unmanaged is held, so
///         a font that is simply dropped costs nothing beyond waiting for the collector.
///     </para>
/// </summary>
public sealed class MsdfFont : IDisposable
{
    /// <summary>
    ///     Rendering at <c>Size == UnitsPerEm</c> with this DPI makes one SixLabors output unit
    ///     equal one font unit (SixLabors: px = size × dpi / 72), which the glyph renderer then
    ///     divides down to ems. Rendering at size 1 would reach em space directly but throw away
    ///     most of the outline's precision on the way.
    /// </summary>
    private const float RenderDpi = 72f;

    /// <summary>
    ///     How sharp a join has to be, in radians, before edge colouring treats it as a corner
    ///     to be kept sharp rather than a smooth continuation.
    /// </summary>
    private const double AngleThreshold = 3.0;

    /// <summary>
    ///     Reusable scratch, borrowed for the duration of a <see cref="TryGenerate" /> call and
    ///     handed back afterwards, so generating a glyph allocates nothing in steady state.
    ///     <para>
    ///         Bags on the instance rather than <c>[ThreadStatic]</c> or <c>static</c> fields:
    ///         concurrent callers still never block, but the buffers belong to this font and go
    ///         away with it instead of being stranded on a thread or held for the process.
    ///     </para>
    /// </summary>
    private readonly ConcurrentBag<ShapeDistanceFinder> _finders = [];

    private readonly ConcurrentBag<Shape> _shapes = [];

    private readonly Font _font;
    private readonly FontMetrics _fontMetrics;

    private volatile bool _disposed;

    private MsdfFont(FontFamily family)
    {
        // Size is chosen after metrics are known, so create a probe font just to read UnitsPerEm.
        var unitsPerEm = family.CreateFont(16).FontMetrics.UnitsPerEm;
        _font = family.CreateFont(unitsPerEm);
        _fontMetrics = _font.FontMetrics;

        Metrics = new MsdfFontMetrics(
            _fontMetrics.UnitsPerEm,
            _fontMetrics.HorizontalMetrics.Ascender,
            _fontMetrics.HorizontalMetrics.Descender,
            _fontMetrics.HorizontalMetrics.LineHeight,
            _fontMetrics.UnderlinePosition,
            _fontMetrics.UnderlineThickness);
    }

    /// <summary>Font-wide vertical metrics, in raw font units.</summary>
    public MsdfFontMetrics Metrics { get; }

    /// <summary>
    ///     Releases the reusable scratch this font has accumulated and stops it generating.
    ///     <para>
    ///         There is no unmanaged resource behind a font, so this is a way to hand memory back
    ///         early rather than an obligation; a font that is never disposed leaks nothing.
    ///         Calling it twice is harmless, and <see cref="Metrics" /> keeps working afterwards.
    ///     </para>
    ///     <para>
    ///         Do not dispose a font while another thread is still generating from it: that call
    ///         either finishes or throws depending on how far it got, which is the usual race on
    ///         disposal and not something the font tries to arbitrate.
    ///     </para>
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _shapes.Clear();
        _finders.Clear();
    }

    /// <summary>Parses a font from a TrueType or OpenType stream. The stream is read in full here.</summary>
    public static MsdfFont Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var collection = new FontCollection();
        return new MsdfFont(collection.Add(stream));
    }

    /// <summary>Parses a font from the bytes of a TrueType or OpenType file.</summary>
    public static MsdfFont Load(ReadOnlySpan<byte> ttf)
    {
        using var stream = new MemoryStream([.. ttf], false);
        return Load(stream);
    }

    /// <summary>
    ///     Generates a 3-channel MSDF for <paramref name="characters" /> (surrogate pairs
    ///     included) into <paramref name="destination" />, which must hold
    ///     <c>size × size × channels</c> floats. Any channel beyond the third is left untouched,
    ///     so the caller can write straight into an RGBA buffer.
    /// </summary>
    /// <returns>
    ///     False when the input decodes to no codepoint at all; <paramref name="destination" /> is then
    ///     untouched. A codepoint the font does not cover still generates, as its <c>.notdef</c> glyph.
    /// </returns>
    public bool TryGenerate(ReadOnlySpan<char> characters, Span<float> destination,
        int size, int channels, double pxRange, out MsdfGlyph glyph)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 3);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        if (destination.Length < size * size * channels)
            throw new ArgumentException($"Need {size * size * channels} floats, got {destination.Length}.",
                nameof(destination));

        glyph = default;

        if (!_shapes.TryTake(out var shape)) shape = new Shape();
        if (!_finders.TryTake(out var finder)) finder = new ShapeDistanceFinder();

        try
        {
            if (!TryBuildShape(characters, shape, out var advance)) return false;

            PrepareShape(shape, finder);

            var (translate, scale) = AutoFrame(shape, size, pxRange);

            var transformation = new SdfTransformation(
                new Projection(scale, translate),
                new DistanceMapping(new DistanceRange(pxRange)));

            MsdfGenerator.Generate(destination, size, channels, shape, transformation, finder, _finders);

            glyph = new MsdfGlyph(advance,
                new System.Numerics.Vector2((float)translate.X, (float)translate.Y),
                new System.Numerics.Vector2((float)scale.X, (float)scale.Y));
            return true;
        }
        finally
        {
            _shapes.Add(shape);
            _finders.Add(finder);
        }
    }

    /// <summary>
    ///     Makes sure the outline is wound the right way round, by sampling a point far outside
    ///     the shape's bounds: if that point reads as <i>inside</i>, the whole glyph is inside-out
    ///     and every contour is reversed. Fonts do not agree on winding direction, and getting it
    ///     wrong turns a glyph into its own negative.
    /// </summary>
    private static void FixGeometry(Shape shape, ShapeDistanceFinder finder)
    {
        var bounds = shape.GetBounds();
        var outerPoint = new Vector2d(
            bounds.L - (bounds.R - bounds.L) - 1,
            bounds.B - (bounds.T - bounds.B) - 1);

        finder.Reset(shape.EdgeCount);
        if (finder.TrueDistance(shape, outerPoint) > 0) shape.ReverseAllContours();
    }

    /// <summary>
    ///     Gets a freshly built outline ready to rasterise: enough edges per contour to colour,
    ///     the right winding, and a colour on every edge. The second normalise is not redundant —
    ///     reversing contours can leave one back in a state colouring cannot handle.
    ///     <para>
    ///         ponytail: contours are not re-oriented against each other, which would fix a glyph
    ///         whose holes are wound the same way as its outline. That needs scanline
    ///         intersections; add it if a font ever renders with filled counters.
    ///     </para>
    /// </summary>
    private static void PrepareShape(Shape shape, ShapeDistanceFinder finder)
    {
        shape.Normalize();
        FixGeometry(shape, finder);
        shape.Normalize();
        EdgeColoring.Simple(shape, AngleThreshold);
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

        // ColorFontSupport.None: we want the base outline, never the colour layers of an emoji
        // font — those are a stack of filled shapes, not one outline to take a distance from.
        if (!_fontMetrics.TryGetGlyphMetrics(codePoint, TextAttributes.None, TextDecorations.None,
                LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out var glyphMetrics))
            return false;

        advance = glyphMetrics.AdvanceWidth / (double)_fontMetrics.UnitsPerEm;

        var renderer = new OutlineGlyphRenderer(shape, _fontMetrics.UnitsPerEm);
        var options = new TextOptions(_font)
        {
            Dpi = RenderDpi,
            Origin = System.Numerics.Vector2.Zero,
            ColorFontSupport = ColorFontSupport.None
        };

        TextRenderer.RenderTextTo(renderer, text, options);

        // A glyph with metrics but no ink — space, above all — is not a failure: it still has to
        // advance the pen, and it rasterises to a field that is everywhere outside. Nothing to
        // anchor, though, since an empty shape's bounds are still the sentinels.
        if (shape.EdgeCount == 0) return true;

        AnchorToBaseline(shape, glyphMetrics);
        shape.Normalize();
        return true;
    }

    /// <summary>
    ///     Moves the rendered outline so that it sits on the baseline, where the top of its ink
    ///     is the height the font's own metrics give it:
    ///     <c>Ascender − TopSideBearing</c>.
    ///     <para>
    ///         This has to be measured from the glyph's own metrics rather than taken from where
    ///         SixLabors put it, because SixLabors has no fixed baseline to place against: it
    ///         grows its line box for ink rising above the ascender, which pushes tall glyphs
    ///         down by their own overshoot. Both sides of the correction use our own curve
    ///         bounds, so it does not depend on how SixLabors measures ink either.
    ///     </para>
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
    private static bool TryFirstCodePoint(ReadOnlySpan<char> characters, out CodePoint codePoint,
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

    /// <summary>
    ///     Fits the shape into a <paramref name="size" />-square bitmap, centred on its shorter
    ///     axis and leaving <paramref name="pxRange" /> pixels of margin for the distance field to
    ///     fall off in. A shape with no area is framed as the unit square instead.
    ///     <para>
    ///         The translate and scale returned are what the caller must apply to place the
    ///         rendered bitmap, so they are part of the output, not scratch.
    ///     </para>
    /// </summary>
    internal static (Vector2d Translate, Vector2d Scale) AutoFrame(Shape shape, int size, double pxRange)
    {
        var translate = Vector2d.Zero;
        var scale = new Vector2d(1, 1);

        var bounds = Bounds.Empty;
        shape.Bound(ref bounds.L, ref bounds.B, ref bounds.R, ref bounds.T);

        double l = bounds.L, b = bounds.B, r = bounds.R, t = bounds.T;
        if (l >= r || b >= t)
        {
            l = 0;
            b = 0;
            r = 1;
            t = 1;
        }

        var frame = new Vector2d(size - pxRange, size - pxRange);
        if (frame.X <= 0 || frame.Y <= 0) return (translate, scale);

        var dims = new Vector2d(r - l, t - b);

        if (dims.X * frame.Y < dims.Y * frame.X)
        {
            var fitScale = frame.Y / dims.Y;
            translate = new Vector2d(0.5 * (frame.X / frame.Y * dims.Y - dims.X) - l, -b);
            scale = new Vector2d(fitScale, fitScale);
        }
        else
        {
            var fitScale = frame.X / dims.X;
            translate = new Vector2d(-l, 0.5 * (frame.Y / frame.X * dims.X - dims.Y) - b);
            scale = new Vector2d(fitScale, fitScale);
        }

        translate += new Vector2d(pxRange / 2 / scale.X, pxRange / 2 / scale.Y);
        return (translate, scale);
    }
}

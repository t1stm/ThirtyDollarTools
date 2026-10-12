using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Sundex.Slug.Geometry;

namespace Sundex.Slug;

/// <summary>
///     A whole font as Slug curve and band data: two RGBA32F textures' worth of floats, plus the
///     per-glyph record that points into them. Built once per font, up front, for every codepoint
///     the font maps; nothing is generated on demand afterwards.
///     <para>
///         Plain arrays, no GL: the engine wraps <see cref="Curves" /> and <see cref="Bands" /> in
///         textures <see cref="TextureWidth" /> texels wide and <see cref="CurveRows" /> /
///         <see cref="BandRows" /> tall. The layout follows Eric Lengyel's reference Slug shaders;
///         see <c>docs/handover/sundex-slug-plan.md</c>, "Data layout".
///     </para>
/// </summary>
public sealed class SlugFontData
{
    /// <summary>Both textures' width in texels: the reference shader's <c>kLogBandTextureWidth 12</c>.</summary>
    public const int TextureWidth = 4096;

    /// <summary>
    ///     How far, in ems, a glyph's quad reaches past its ink on every side. Slug anti-aliases
    ///     across a half-pixel fringe outside the ink, so the quad has to cover it.
    ///     <para>
    ///         ponytail: a fixed pad, enough down to 8 px text. Port the reference <c>SlugDilate</c>
    ///         (per vertex, from the MVP and viewport) if smaller text shows clipped edges.
    ///     </para>
    /// </summary>
    public const float QuadPadding = 1 / 16f;

    /// <summary>How far, in ems, bands overlap, so a curve on a band boundary lands in both.</summary>
    internal const double BandEpsilon = 1 / 1024.0;

    /// <summary>The most bands a glyph may use on either axis.</summary>
    internal const int MaxBands = 16;

    private const int FloatsPerTexel = 4;
    private const int FloatsPerRow = TextureWidth * FloatsPerTexel;

    private readonly Dictionary<int, SlugGlyph> _glyphs;

    private SlugFontData(float[] curves, float[] bands, Dictionary<int, SlugGlyph> glyphs, SlugGlyph notDef)
    {
        Curves = curves;
        Bands = bands;
        _glyphs = glyphs;
        NotDef = notDef;
    }

    /// <summary>
    ///     The curve texture: each quadratic is two texels, <c>(p1, p2)</c> then <c>(p3, –)</c>, and
    ///     consecutive curves of a contour share the texel in between.
    /// </summary>
    public float[] Curves { get; }

    /// <summary>The band texture: per glyph, its band headers then its curve lists. Only <c>.xy</c> is used.</summary>
    public float[] Bands { get; }

    public int CurveRows => Curves.Length / FloatsPerRow;
    public int BandRows => Bands.Length / FloatsPerRow;

    /// <summary>How many codepoints map to a glyph of their own, not counting <see cref="NotDef" />.</summary>
    public int GlyphCount => _glyphs.Count;

    /// <summary>The font's <c>.notdef</c> glyph, which every codepoint the font lacks draws as.</summary>
    public SlugGlyph NotDef { get; }

    /// <summary>Builds every codepoint <paramref name="font" /> maps, plus its <c>.notdef</c>.</summary>
    public static SlugFontData Build(SlugFont font)
    {
        var runes = new List<Rune>();
        foreach (var codePoint in font.GetAvailableCodePoints().Span)
            if (Rune.TryCreate(codePoint.Value, out var rune))
                runes.Add(rune);

        // The font hands .notdef out for any codepoint it lacks, so ask for one; the private use
        // area is the likeliest place to find a gap.
        var available = runes.Select(rune => rune.Value).ToHashSet();
        var missing = 0xE000;
        while (available.Contains(missing)) missing++;
        runes.Add(new Rune(missing));

        // Outlines in parallel: the font is safe to share and each outline is independent.
        // Packing them stays sequential, since every glyph's place depends on the ones before
        // it. Half the machine at most, as the caller is a boot that is still drawing.
        var shapes = new Shape?[runes.Count];
        var advances = new double[runes.Count];
        Parallel.For(0, runes.Count,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) },
            i =>
            {
                Span<char> characters = stackalloc char[2];
                var length = runes[i].EncodeToUtf16(characters);
                var shape = new Shape();
                if (font.TryBuildShape(characters[..length], shape, out advances[i])) shapes[i] = shape;
            });

        var builder = new Builder();
        var glyphs = new Dictionary<int, SlugGlyph>();
        for (var i = 0; i < runes.Count - 1; i++)
            if (shapes[i] is { } shape)
                glyphs[runes[i].Value] = builder.Add(shape, advances[i]);

        var notDef = builder.Add(shapes[^1] ?? new Shape(), shapes[^1] is null ? 0 : advances[^1]);
        return builder.Finish(glyphs, notDef);
    }

    /// <summary>
    ///     The glyph for the first codepoint in <paramref name="characters" /> (a surrogate pair
    ///     counts as one; NUL padding is ignored), or <see cref="NotDef" /> when the font lacks it
    ///     or the input decodes to nothing.
    /// </summary>
    public SlugGlyph GetGlyph(ReadOnlySpan<char> characters)
    {
        return SlugFont.TryFirstCodePoint(characters, out var codePoint, out _) &&
               _glyphs.TryGetValue(codePoint.Value, out var glyph)
            ? glyph
            : NotDef;
    }

    /// <summary>One quadratic as stored, and where its first texel sits in the curve texture.</summary>
    private readonly record struct Curve(Vector2 P1, Vector2 P2, Vector2 P3, int X, int Y)
    {
        public float MinX => MathF.Min(MathF.Min(P1.X, P2.X), P3.X);
        public float MaxX => MathF.Max(MathF.Max(P1.X, P2.X), P3.X);
        public float MinY => MathF.Min(MathF.Min(P1.Y, P2.Y), P3.Y);
        public float MaxY => MathF.Max(MathF.Max(P1.Y, P2.Y), P3.Y);

        public bool IsHorizontalLine => P1.Y == P2.Y && P2.Y == P3.Y;
        public bool IsVerticalLine => P1.X == P2.X && P2.X == P3.X;
    }

    /// <summary>
    ///     Appends glyphs to the two textures one at a time. Internal so tests can feed it
    ///     synthetic shapes as well as font outlines.
    /// </summary>
    internal sealed class Builder
    {
        private readonly List<float> _bands = [];
        private readonly List<float> _curves = [];
        private readonly List<Curve> _glyphCurves = [];
        private readonly List<int>[] _lists = new List<int>[MaxBands];

        /// <summary>Scratch for <see cref="ChooseBandCount" />, grown to the largest glyph seen.</summary>
        private double[] _lo = [], _hi = [], _first = [], _last = [];

        /// <summary>The run of curves being collected: their start and control points, and where it ends.</summary>
        private readonly List<Vector2> _run = [];

        private Vector2 _runEnd;

        public Builder()
        {
            for (var i = 0; i < _lists.Length; i++) _lists[i] = [];
        }

        /// <summary>Writes <paramref name="shape" />'s curves and bands and returns the glyph pointing at them.</summary>
        public SlugGlyph Add(Shape shape, double advance)
        {
            _glyphCurves.Clear();
            for (var c = 0; c < shape.ContourCount; c++)
            {
                foreach (ref var edge in shape.ContourEdges(c)) AddEdge(edge);
                FlushRun();
            }

            if (_glyphCurves.Count == 0)
            {
                // Space and friends: a single empty band each way, so the shader's loops run zero times.
                var emptyLoc = ReserveHeaders(2);
                return new SlugGlyph((float)advance, new Vector4(-QuadPadding, -QuadPadding, QuadPadding, QuadPadding), Vector4.Zero,
                    new Vector4(emptyLoc.X, emptyLoc.Y, 0, 0));
            }

            var bounds = shape.GetBounds();
            float l = (float)bounds.L, b = (float)bounds.B, r = (float)bounds.R, t = (float)bounds.T;

            var nv = ChooseBandCount(l, r - l, vertical: true);
            var nh = ChooseBandCount(b, t - b, vertical: false);

            var loc = ReserveHeaders(nh + nv);
            var glyphStart = loc.Y * TextureWidth + loc.X;

            // Horizontal bands first, then vertical: the shader reads them in that order.
            WriteBands(glyphStart, 0, b, t - b, nh, vertical: false);
            WriteBands(glyphStart, nh, l, r - l, nv, vertical: true);

            return new SlugGlyph((float)advance,
                new Vector4(l - QuadPadding, b - QuadPadding, r + QuadPadding, t + QuadPadding),
                new Vector4(BandScale(nv, r - l), BandScale(nh, t - b),
                    -l * BandScale(nv, r - l), -b * BandScale(nh, t - b)),
                new Vector4(loc.X, loc.Y, nv - 1, nh - 1));
        }

        /// <summary>Pads both textures out to whole rows and hands them over.</summary>
        public SlugFontData Finish(Dictionary<int, SlugGlyph> glyphs, SlugGlyph notDef)
        {
            return new SlugFontData(ToRows(_curves), ToRows(_bands), glyphs, notDef);
        }

        private static float BandScale(int count, float extent)
        {
            // An axis with no extent has one band, and everything maps to it.
            return extent > 0 ? count / extent : 0;
        }

        private void AddEdge(in EdgeSegment edge)
        {
            switch (edge.Kind)
            {
                case EdgeKind.Linear:
                    // Lengyel's README: a line is the quadratic with its second end point doubled.
                    AddQuadratic(ToFloat(edge.P0), ToFloat(edge.P1), ToFloat(edge.P1));
                    return;

                case EdgeKind.Quadratic:
                    AddQuadratic(ToFloat(edge.P0), ToFloat(edge.P1), ToFloat(edge.P2));
                    return;

                default:
                    // ponytail: four quadratics by midpoint approximation, which is plenty for
                    // glyph-sized curves. Every bundled font is TrueType, so this path is unused
                    // today; switch to an error-bounded split if a CFF font is ever bundled.
                    for (var i = 0; i < 4; i++)
                    {
                        CubicPiece(edge, i / 4.0, (i + 1) / 4.0, out var c0, out var c1, out var c2, out var c3);
                        AddQuadratic(ToFloat(c0), ToFloat((3 * (c1 + c2) - (c0 + c3)) / 4), ToFloat(c3));
                    }

                    return;
            }
        }

        /// <summary>The control points of the part of a cubic between parameters <paramref name="t0" /> and <paramref name="t1" />.</summary>
        private static void CubicPiece(in EdgeSegment edge, double t0, double t1,
            out Vector2d c0, out Vector2d c1, out Vector2d c2, out Vector2d c3)
        {
            c0 = edge.Point(t0);
            c3 = edge.Point(t1);
            c1 = c0 + ThirdDerivative(edge, t0) * (t1 - t0);
            c2 = c3 - ThirdDerivative(edge, t1) * (t1 - t0);
        }

        /// <summary>A cubic's exact first derivative over three (unlike <see cref="EdgeSegment.Direction" />, no fallbacks).</summary>
        private static Vector2d ThirdDerivative(in EdgeSegment edge, double t)
        {
            var u = 1 - t;
            return (edge.P1 - edge.P0) * (u * u) + (edge.P2 - edge.P1) * (2 * u * t) + (edge.P3 - edge.P2) * (t * t);
        }

        private void AddQuadratic(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            // Consecutive curves share their texel only when they really share the end point.
            if (_run.Count > 0 && (_runEnd != p1 || _run.Count / 2 == TextureWidth - 1)) FlushRun();

            _run.Add(p1);
            _run.Add(p2);
            _runEnd = p3;
        }

        /// <summary>Writes the collected run as <c>n + 1</c> texels, never across a row end.</summary>
        private void FlushRun()
        {
            if (_run.Count == 0) return;

            var curveCount = _run.Count / 2;
            var start = StartRun(_curves, curveCount + 1);

            for (var i = 0; i < curveCount; i++)
            {
                var p1 = _run[i * 2];
                var p2 = _run[i * 2 + 1];
                var p3 = i + 1 < curveCount ? _run[(i + 1) * 2] : _runEnd;

                AddTexel(_curves, p1.X, p1.Y, p2.X, p2.Y);
                _glyphCurves.Add(new Curve(p1, p2, p3, (start + i) % TextureWidth, (start + i) / TextureWidth));
            }

            AddTexel(_curves, _runEnd.X, _runEnd.Y, 0, 0);
            _run.Clear();
        }

        /// <summary>
        ///     Picks the band count in <c>1..</c><see cref="MaxBands" /> whose longest curve list is
        ///     shortest, preferring fewer bands on a tie.
        ///     <para>
        ///         The hottest loop of the build: every candidate count against every curve. Each curve's
        ///         ε-widened range is worked out once, then each count's band indices and band sizes are
        ///         computed <see cref="Vector{T}" />-wide. It stays in doubles, with the same operations
        ///         in the same order as <see cref="TryGetBandRange" />, so it picks exactly what that
        ///         would.
        ///     </para>
        ///     <para>
        ///         <see cref="MethodImplOptions.AggressiveOptimization" /> on this and its two helpers:
        ///         the build runs at boot, while the JIT still hands out unoptimised tier-0 code, which
        ///         doesn't vectorise. Without it, the first font built sees none of the speed-up.
        ///     </para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private int ChooseBandCount(float min, float extent, bool vertical)
        {
            if (extent <= 0) return 1;

            var capacity = _glyphCurves.Count;
            if (_lo.Length < capacity)
            {
                _lo = new double[capacity];
                _hi = new double[capacity];
                _first = new double[capacity];
                _last = new double[capacity];
            }

            // A line parallel to the bands joins none of them, whatever the count.
            var m = 0;
            foreach (ref readonly var curve in CollectionsMarshal.AsSpan(_glyphCurves))
            {
                if (vertical ? curve.IsVerticalLine : curve.IsHorizontalLine) continue;

                _lo[m] = (double)(vertical ? curve.MinX : curve.MinY) - BandEpsilon - min;
                _hi[m] = (double)(vertical ? curve.MaxX : curve.MaxY) + BandEpsilon - min;
                m++;
            }

            ReadOnlySpan<double> lo = _lo.AsSpan(0, m), hi = _hi.AsSpan(0, m);
            Span<double> first = _first.AsSpan(0, m), last = _last.AsSpan(0, m);
            int best = 1, bestLongest = int.MaxValue;

            for (var n = 1; n <= MaxBands; n++)
            {
                var scale = n / (double)extent;
                BandIndices(lo, scale, n, first);
                BandIndices(hi, scale, n, last);

                var longest = 0;
                for (var band = 0; band < n; band++) longest = Math.Max(longest, CountInBand(first, last, band));

                if (longest >= bestLongest) continue;
                best = n;
                bestLongest = longest;
            }

            return best;
        }

        /// <summary>
        ///     <c>clamp(floor(value · scale), 0, n − 1)</c> for each value: the band an end of a range
        ///     falls in, as a whole-number double.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal static void BandIndices(ReadOnlySpan<double> values, double scale, int n, Span<double> indices)
        {
            var width = Vector<double>.Count;
            var vScale = new Vector<double>(scale);
            var vLast = new Vector<double>(n - 1);

            var i = 0;
            for (; i <= values.Length - width; i += width)
            {
                var index = Vector.Floor(new Vector<double>(values[i..]) * vScale);
                Vector.Min(Vector.Max(index, Vector<double>.Zero), vLast).CopyTo(indices[i..]);
            }

            for (; i < values.Length; i++) indices[i] = Math.Clamp(Math.Floor(values[i] * scale), 0, n - 1);
        }

        /// <summary>How many of the ranges <c>[first, last]</c> include <paramref name="band" />.</summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal static int CountInBand(ReadOnlySpan<double> first, ReadOnlySpan<double> last, int band)
        {
            var width = Vector<double>.Count;
            var vBand = new Vector<double>(band);

            // A comparison sets a lane to all ones, which is -1 as a long, so the sum counts down.
            var negativeCount = Vector<long>.Zero;
            var i = 0;
            for (; i <= first.Length - width; i += width)
                negativeCount += Vector.LessThanOrEqual(new Vector<double>(first[i..]), vBand) &
                                 Vector.GreaterThanOrEqual(new Vector<double>(last[i..]), vBand);

            var count = (int)-Vector.Sum(negativeCount);
            for (; i < first.Length; i++)
                if (first[i] <= band && band <= last[i])
                    count++;

            return count;
        }

        /// <summary>
        ///     Which of <paramref name="n" /> bands a curve's ε-widened range touches, on the x axis
        ///     for vertical bands and the y axis for horizontal ones. False for a straight line
        ///     parallel to the bands, which no ray along them can cross.
        /// </summary>
        private static bool TryGetBandRange(in Curve curve, float min, float extent, int n, bool vertical,
            out int first, out int last)
        {
            first = last = 0;
            if (vertical ? curve.IsVerticalLine : curve.IsHorizontalLine) return false;

            double lo = vertical ? curve.MinX : curve.MinY;
            double hi = vertical ? curve.MaxX : curve.MaxY;
            if (extent <= 0) return true;

            var scale = n / (double)extent;
            first = Math.Clamp((int)Math.Floor((lo - BandEpsilon - min) * scale), 0, n - 1);
            last = Math.Clamp((int)Math.Floor((hi + BandEpsilon - min) * scale), 0, n - 1);
            return true;
        }

        /// <summary>
        ///     Writes <paramref name="n" /> bands' headers (from header <paramref name="headerIndex" />
        ///     on) and their curve lists, each list sorted by the curves' far edge, descending.
        /// </summary>
        private void WriteBands(int glyphStart, int headerIndex, float min, float extent, int n, bool vertical)
        {
            for (var i = 0; i < n; i++) _lists[i].Clear();

            for (var c = 0; c < _glyphCurves.Count; c++)
            {
                if (!TryGetBandRange(_glyphCurves[c], min, extent, n, vertical, out var first, out var last)) continue;
                for (var i = first; i <= last; i++) _lists[i].Add(c);
            }

            // The shader stops walking a list at the first curve wholly behind the pixel, so the
            // curves reaching furthest along the ray have to come first.
            Comparison<int> farthestFirst = vertical
                ? (x, y) => _glyphCurves[y].MaxY.CompareTo(_glyphCurves[x].MaxY)
                : (x, y) => _glyphCurves[y].MaxX.CompareTo(_glyphCurves[x].MaxX);

            for (var i = 0; i < n; i++)
            {
                var list = _lists[i];
                list.Sort(farthestFirst);

                // The shader reads a list's entries along one row without wrapping, so a list
                // never crosses a row end; only where it starts is relative to the glyph.
                var offset = list.Count == 0 ? 0 : StartRun(_bands, list.Count) - glyphStart;
                foreach (var c in list) AddTexel(_bands, _glyphCurves[c].X, _glyphCurves[c].Y, 0, 0);

                var header = (glyphStart + headerIndex + i) * FloatsPerTexel;
                _bands[header] = list.Count;
                _bands[header + 1] = offset;
            }
        }

        /// <summary>Appends zeroed header texels, never across a row end, and returns where they start.</summary>
        private (int X, int Y) ReserveHeaders(int count)
        {
            var start = StartRun(_bands, count);
            for (var i = 0; i < count; i++) AddTexel(_bands, 0, 0, 0, 0);
            return (start % TextureWidth, start / TextureWidth);
        }

        /// <summary>
        ///     Moves to the next row if <paramref name="length" /> texels would not fit in the rest of
        ///     this one, and returns the texel index the run starts at.
        /// </summary>
        private static int StartRun(List<float> texture, int length)
        {
            var start = texture.Count / FloatsPerTexel;
            if (start % TextureWidth + length <= TextureWidth) return start;

            var nextRow = (start / TextureWidth + 1) * TextureWidth;
            for (var i = start; i < nextRow; i++) AddTexel(texture, 0, 0, 0, 0);
            return nextRow;
        }

        private static void AddTexel(List<float> texture, float x, float y, float z, float w)
        {
            texture.Add(x);
            texture.Add(y);
            texture.Add(z);
            texture.Add(w);
        }

        private static float[] ToRows(List<float> texture)
        {
            var rows = Math.Max(1, (texture.Count + FloatsPerRow - 1) / FloatsPerRow);
            var data = new float[rows * FloatsPerRow];
            texture.CopyTo(data);
            return data;
        }

        private static Vector2 ToFloat(Vector2d p)
        {
            return new Vector2((float)p.X, (float)p.Y);
        }
    }
}

using System.Numerics;
using Sundex.Slug.Geometry;

namespace Sundex.Slug.Tests;

/// <summary>
///     Renders Slug glyph data through <see cref="SlugReference" /> and checks it against a ground
///     truth: the outline flattened to line segments and point-tested by the non-zero winding rule
///     at every pixel centre. Includes overlap and winding cases, which a distance field gets wrong.
/// </summary>
public class SlugCoverageTests
{
    /// <summary>Segments each quadratic or cubic is flattened into for the ground truth.</summary>
    private const int FlattenSteps = 16;

    [Theory]
    [InlineData(TestFonts.LatoRegular, 16)]
    [InlineData(TestFonts.LatoRegular, 48)]
    [InlineData(TestFonts.LatoBold, 16)]
    [InlineData(TestFonts.LatoBold, 48)]
    public void PrintableAsciiMatchesTheOutline(string fontName, float pixelsPerEm)
    {
        var font = TestFonts.Shared(fontName);
        var data = font.SlugData;
        var shape = new Shape();

        int mismatched = 0, counted = 0;
        var worst = new List<(char Character, int Mismatched)>();

        for (var c = '!'; c <= '~'; c++)
        {
            Assert.True(font.TryBuildShape([c], shape, out _));
            var (glyphMismatched, glyphCounted) = Compare(data, data.GetGlyph([c]), shape, pixelsPerEm);

            mismatched += glyphMismatched;
            counted += glyphCounted;
            if (glyphMismatched > 0) worst.Add((c, glyphMismatched));
        }

        var rate = mismatched / (double)counted;
        Assert.True(rate <= 0.005,
            $"{mismatched} of {counted} pixels ({rate:P2}) disagree with the outline; worst: " +
            string.Join(", ", worst.OrderByDescending(w => w.Mismatched).Take(8).Select(w => $"'{w.Character}'={w.Mismatched}")));
    }

    /// <summary>Two same-wound squares that overlap render as their union. A distance field can't.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OverlappingContoursRenderAsTheirUnion(bool counterClockwise)
    {
        var shape = new Shape();
        AddSquare(shape, 0, 0, 1, 1, counterClockwise);
        AddSquare(shape, 0.5, 0.5, 1.5, 1.5, counterClockwise);

        var (data, glyph) = BuildOne(shape);

        Assert.Equal(1f, CoverageAt(data, glyph, 0.75f, 0.75f)); // in both
        Assert.Equal(1f, CoverageAt(data, glyph, 0.25f, 0.25f)); // first only
        Assert.Equal(1f, CoverageAt(data, glyph, 1.25f, 1.25f)); // second only
        Assert.Equal(0f, CoverageAt(data, glyph, 1.25f, 0.25f)); // neither
        AssertMatchesOutline(data, glyph, shape);
    }

    [Fact]
    public void OppositelyWoundInnerContourIsAHole()
    {
        var shape = new Shape();
        AddSquare(shape, 0, 0, 1, 1, true);
        AddSquare(shape, 0.25, 0.25, 0.75, 0.75, false);

        var (data, glyph) = BuildOne(shape);

        Assert.Equal(0f, CoverageAt(data, glyph, 0.5f, 0.5f));
        Assert.Equal(1f, CoverageAt(data, glyph, 0.1f, 0.5f));
        AssertMatchesOutline(data, glyph, shape);
    }

    /// <summary>The non-zero rule: a same-wound contour inside another adds to the winding, it does not cut a hole.</summary>
    [Fact]
    public void SameWoundInnerContourIsSolid()
    {
        var shape = new Shape();
        AddSquare(shape, 0, 0, 1, 1, true);
        AddSquare(shape, 0.25, 0.25, 0.75, 0.75, true);

        var (data, glyph) = BuildOne(shape);

        Assert.Equal(1f, CoverageAt(data, glyph, 0.5f, 0.5f));
        AssertMatchesOutline(data, glyph, shape);
    }

    /// <summary>A pixel straddling an edge gets partial coverage: the half-pixel anti-aliasing fringe.</summary>
    [Fact]
    public void EdgePixelsArePartiallyCovered()
    {
        var shape = new Shape();
        AddSquare(shape, 0, 0, 1, 1, true);

        var (data, glyph) = BuildOne(shape);

        // 32 px per em, sampled a quarter pixel inside the left edge.
        var coverage = CoverageAt(data, glyph, 0.25f / 32, 0.5f);
        Assert.InRange(coverage, 0.7f, 0.8f);
    }

    [Fact]
    public void UnmappedCodepointDrawsNotDef()
    {
        var data = TestFonts.Shared(TestFonts.LatoRegular).SlugData;

        var glyph = data.GetGlyph("");

        Assert.Equal(data.NotDef, glyph);
        Assert.True(SlugDataTests.Decode(data, glyph).CurveCount > 0, ".notdef should have an outline");
        Assert.True(glyph.Advance > 0);
    }

    [Theory]
    [InlineData(' ')]
    [InlineData(' ')] // no-break space
    public void BlankGlyphHasAnAdvanceAndNoCurves(char character)
    {
        var data = TestFonts.Shared(TestFonts.LatoRegular).SlugData;

        var glyph = data.GetGlyph([character]);

        Assert.NotEqual(data.NotDef, glyph);
        Assert.True(glyph.Advance > 0, "a blank glyph still advances the pen");
        Assert.Equal(0, SlugDataTests.Decode(data, glyph).CurveCount);
        Assert.Equal(0f, CoverageAt(data, glyph, 0, 0));
    }

    [Fact]
    public void EmptyInputDrawsNotDef()
    {
        var data = TestFonts.Shared(TestFonts.LatoRegular).SlugData;

        Assert.Equal(data.NotDef, data.GetGlyph([]));
        Assert.Equal(data.NotDef, data.GetGlyph("\0\0"));
    }

    [Fact]
    public void SurrogatePairDecodesAsOneCodepoint()
    {
        var data = TestFonts.Shared(TestFonts.Twemoji).SlugData;

        // U+1F600 is in the font (its base outline is empty, so it draws nothing): it must not
        // fall back to .notdef the way a lone high surrogate does.
        Assert.NotEqual(data.NotDef, data.GetGlyph("\U0001F600"));
        Assert.Equal(data.NotDef, data.GetGlyph("\uD83D"));
    }

    internal static (SlugFontData Data, SlugGlyph Glyph) BuildOne(Shape shape)
    {
        var builder = new SlugFontData.Builder();
        var glyph = builder.Add(shape, 1);
        return (builder.Finish([], glyph), glyph);
    }

    private static void AssertMatchesOutline(SlugFontData data, SlugGlyph glyph, Shape shape)
    {
        var (mismatched, counted) = Compare(data, glyph, shape, 32);
        Assert.True(counted > 0);
        Assert.Equal(0, mismatched);
    }

    private static float CoverageAt(SlugFontData data, SlugGlyph glyph, float x, float y) =>
        SlugReference.SlugRender(data, glyph, new Vector2(x, y), new Vector2(32));

    private static void AddSquare(Shape shape, double x0, double y0, double x1, double y1, bool counterClockwise)
    {
        Vector2d[] corners = [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];
        if (!counterClockwise) Array.Reverse(corners);

        shape.BeginContour();
        for (var i = 0; i < corners.Length; i++)
            shape.AddEdge(EdgeSegment.Linear(corners[i], corners[(i + 1) % corners.Length]));
        shape.EndContour();
    }

    /// <summary>
    ///     Renders <paramref name="glyph" /> over its whole quad and counts the pixels whose
    ///     thresholded coverage disagrees with the winding test, skipping those within a pixel of
    ///     an edge, where anti-aliasing legitimately decides either way.
    /// </summary>
    private static (int Mismatched, int Counted) Compare(SlugFontData data, SlugGlyph glyph,
        Shape shape, float pixelsPerEm)
    {
        var segments = Flatten(shape);
        var rect = glyph.EmRect;
        var width = (int)MathF.Ceiling((rect.Z - rect.X) * pixelsPerEm);
        var height = (int)MathF.Ceiling((rect.W - rect.Y) * pixelsPerEm);
        var nearEdge = 1.0 / pixelsPerEm;

        int mismatched = 0, counted = 0;
        for (var py = 0; py < height; py++)
        for (var px = 0; px < width; px++)
        {
            var sample = new Vector2(rect.X + (px + 0.5f) / pixelsPerEm, rect.Y + (py + 0.5f) / pixelsPerEm);
            var point = new Vector2d(sample.X, sample.Y);

            if (DistanceToOutline(segments, point) < nearEdge) continue;

            counted++;
            var covered = SlugReference.SlugRender(data, glyph, sample, new Vector2(pixelsPerEm)) >= 0.5f;
            if (covered != (Winding(segments, point) != 0)) mismatched++;
        }

        return (mismatched, counted);
    }

    private static List<(Vector2d A, Vector2d B)> Flatten(Shape shape)
    {
        var segments = new List<(Vector2d, Vector2d)>();
        foreach (var edge in shape.Edges)
        {
            var steps = edge.Kind == EdgeKind.Linear ? 1 : FlattenSteps;
            for (var i = 0; i < steps; i++)
                segments.Add((edge.Point(i / (double)steps), edge.Point((i + 1) / (double)steps)));
        }

        return segments;
    }

    /// <summary>The non-zero winding number of the outline around <paramref name="p" />.</summary>
    private static int Winding(List<(Vector2d A, Vector2d B)> segments, Vector2d p)
    {
        var winding = 0;
        foreach (var (a, b) in segments)
        {
            var side = Vector2d.Cross(b - a, p - a);
            if (a.Y <= p.Y)
            {
                if (b.Y > p.Y && side > 0) winding++;
            }
            else if (b.Y <= p.Y && side < 0)
            {
                winding--;
            }
        }

        return winding;
    }

    private static double DistanceToOutline(List<(Vector2d A, Vector2d B)> segments, Vector2d p)
    {
        var best = double.MaxValue;
        foreach (var (a, b) in segments)
        {
            var ab = b - a;
            var lengthSquared = Vector2d.Dot(ab, ab);
            var t = lengthSquared > 0 ? Math.Clamp(Vector2d.Dot(p - a, ab) / lengthSquared, 0, 1) : 0;
            best = Math.Min(best, (a + ab * t - p).Length);
        }

        return best;
    }
}

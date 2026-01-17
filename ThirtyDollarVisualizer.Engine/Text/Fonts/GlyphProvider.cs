using System.Runtime.InteropServices;
using Msdfgen;
using Msdfgen.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Range = Msdfgen.Range;

namespace ThirtyDollarVisualizer.Engine.Text.Fonts;

public class GlyphProvider
{
    public GlyphProvider(FontProvider fontProvider, string fontName)
    {
        Font = fontProvider.GetFont(fontName);
        FontLoader.GetFontMetrics(out var metrics, Font);
        FontMetrics = metrics;
    }

    public const int GlyphSize = 48;
    public const float MsdfRange = 4.0f;
    public FontHandle Font { get; }
    public FontMetrics FontMetrics { get; }
    protected Dictionary<string, TextAlignmentData> SizingData { get; } = new();

    private static void FixGeometry(Shape shape)
    {
        var bounds = shape.GetBounds();
        Vector2 outerPoint = new(
            bounds.L - (bounds.R - bounds.L) - 1,
            bounds.B - (bounds.T - bounds.B) - 1
        );

        var combiner = new SimpleContourCombiner<TrueDistanceSelector>(shape);
        var finder = new ShapeDistanceFinder<SimpleContourCombiner<TrueDistanceSelector>>(shape, combiner);
        double distance = finder.Distance(outerPoint);

        if (!(distance > 0)) return;
        foreach (var contour in shape.Contours)
            contour.Reverse();
    }
    private static (Vector2 translate, Vector2 scale) AutoFrame(Shape shape)
    {
        const double pxRange = MsdfRange;

        var translate = new Vector2(0, 0);
        var scale = new Vector2(1, 1);

        double l = 1e240, b = 1e240, r = -1e240, t = -1e240;
        shape.Bound(ref l, ref b, ref r, ref t);

        if (l >= r || b >= t)
        {
            l = 0;
            b = 0;
            r = 1;
            t = 1;
        }

        var frame = new Vector2(GlyphSize, GlyphSize);
        frame = new Vector2(frame.X - pxRange, frame.Y - pxRange);

        if (frame.X <= 0 || frame.Y <= 0)
        {
            return (translate, scale);
        }

        var dims = new Vector2(r - l, t - b);

        if (dims.X * frame.Y < dims.Y * frame.X)
        {
            var fitScale = frame.Y / dims.Y;
            translate = new Vector2(0.5 * (frame.X / frame.Y * dims.Y - dims.X) - l, -b);
            scale = new Vector2(fitScale, fitScale);
        }
        else
        {
            var fitScale = frame.X / dims.X;
            translate = new Vector2(-l, 0.5 * (frame.Y / frame.X * dims.X - dims.Y) - b);
            scale = new Vector2(fitScale, fitScale);
        }

        translate +=
            new Vector2(pxRange / 2 / scale.X, pxRange / 2 / scale.Y);

        return (translate, scale);
    }

    public Image<RgbaVector> GetGlyph(ReadOnlySpan<char> character)
    {
        const double rangeSymmetricalWidth = MsdfRange;
        var shape = new Shape();

        var charBytes = MemoryMarshal.AsBytes(character);
        Span<uint> charUintSpan = stackalloc uint[1];
        charBytes.CopyTo(MemoryMarshal.AsBytes(charUintSpan));

        var glyph = charUintSpan[0];
        FontLoader.LoadGlyph(shape, Font, glyph, FontCoordinateScaling.EmNormalized, out var advance);

        if (!shape.Validate())
            throw new Exception("Invalid shape.");

        shape.Normalize();
        FixGeometry(shape);

        shape.OrientContours();
        shape.Normalize();

        var (translate, scale) = AutoFrame(shape);

        const double angleThreshold = 3d;
        EdgeColoring.EdgeColoringSimple(shape, angleThreshold);

        var range = new Range(rangeSymmetricalWidth);
        var projection = new SDFTransformation(new Projection(scale, translate),
            new DistanceMapping(range));

        const int channels = 4;
        var bitmap = new Bitmap<float>(GlyphSize, GlyphSize, channels);
        MsdfGenerator.GenerateMTSDF(bitmap, shape, projection, new MSDFGeneratorConfig());

        var rgbaSlice = bitmap.Pixels.AsSpan();
        var array = new RgbaVector[rgbaSlice.Length / channels];

        for (var i = 0; i < array.Length; i++)
        {
            array[i] = new RgbaVector
            {
                R = rgbaSlice[i * channels],
                G = rgbaSlice[i * channels + 1],
                B = rgbaSlice[i * channels + 2],
                A = rgbaSlice[i * channels + 3]
            };
        }

        var image = Image.WrapMemory<RgbaVector>(Configuration.Default, array, GlyphSize, GlyphSize);
        lock (SizingData)
        {
            var lookup = SizingData.GetAlternateLookup<ReadOnlySpan<char>>();
            lookup.TryAdd(character, new TextAlignmentData
            {
                AdvanceInUnitSpace = advance,
                Scale = scale,
                Translate = translate
            });
        }

        return image;
    }

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
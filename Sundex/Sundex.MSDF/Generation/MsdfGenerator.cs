using System.Collections.Concurrent;
using Sundex.MSDF.Distance;
using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Generation;

/// <summary>
///     Rasterises a shape into a multi-channel distance field: for each pixel, the signed
///     distance to the shape in each of the three channels, mapped onto 0..1.
///     <para>
///         This is where nearly all the time in generating a glyph goes, so it is arranged to
///         allocate nothing: the caller owns the output span and the
///         <see cref="ShapeDistanceFinder" />s, and the loop only writes.
///     </para>
///     <para>
///         ponytail: no error-correction pass. That pass hunts down isolated pixels whose median
///         disagrees with their neighbours, which the wide distance range used here makes smooth
///         enough not to produce. Add one if speckles ever appear, or if the range is tightened
///         toward the conventional few pixels' worth.
///     </para>
/// </summary>
internal static class MsdfGenerator
{
    /// <summary>
    ///     Rows per parallel band. Each band needs its own <see cref="ShapeDistanceFinder" /> and
    ///     opens on a cold distance bound, so bands trade one slow pixel for the parallelism —
    ///     small enough here to keep every core busy at a 48px glyph, large enough that the cold
    ///     pixel is amortised over three rows.
    /// </summary>
    private const int RowsPerBand = 3;

    /// <summary>
    ///     Below this many pixels the work is not worth handing to the thread pool.
    /// </summary>
    private const int ParallelPixelThreshold = 1024;

    /// <summary>
    ///     Writes a 3-channel MSDF into <paramref name="output" /> with the given
    ///     <paramref name="channels" /> stride, leaving any extra channel untouched.
    ///     <para>
    ///         <paramref name="finderPool" /> supplies the extra finders the parallel bands need —
    ///         one per band, each carrying an edge-cache array sized to the glyph, which is worth
    ///         reusing. It belongs to the caller, so nothing outlives the font it came from.
    ///     </para>
    /// </summary>
    public static void Generate(Span<float> output, int size, int channels, Shape shape,
        in SdfTransformation transformation, ShapeDistanceFinder finder,
        ConcurrentBag<ShapeDistanceFinder> finderPool)
    {
        if (size * size < ParallelPixelThreshold)
        {
            finder.Reset(shape.EdgeCount);
            GenerateRows(output, 0, size, size, channels, shape, transformation, finder);
            return;
        }

        GenerateParallel(output, size, channels, shape, transformation, finderPool);
    }

    /// <summary>
    ///     Rasterises rows <paramref name="startRow" /> (inclusive) to <paramref name="endRow" />
    ///     (exclusive). The caller owns resetting <paramref name="finder" /> for the shape.
    /// </summary>
    private static void GenerateRows(Span<float> output, int startRow, int endRow, int size, int channels,
        Shape shape, in SdfTransformation transformation, ShapeDistanceFinder finder)
    {
        // Alternate rows are scanned right-to-left so that consecutive samples stay adjacent and
        // the edge cache stays useful across the row boundary. Purely a speed trick: the cache's
        // bound only ever loosens with distance, so the result does not depend on visit order.
        var rightToLeft = false;

        // Row 0 is the BOTTOM of the glyph, so the buffer comes out vertically flipped from an
        // image's point of view. That is deliberate: the engine hands this straight to an OpenGL
        // texture, whose V axis runs bottom-up and flips it back on sampling. Writing it top-down
        // instead renders every vertically asymmetric glyph upside down.
        for (var y = startRow; y < endRow; y++)
        {
            for (var col = 0; col < size; col++)
            {
                var x = rightToLeft ? size - col - 1 : col;

                var p = transformation.Unproject(new Vector2d(x + .5, y + .5));
                var distance = finder.MultiDistance(shape, p);

                var i = (y * size + x) * channels;
                output[i] = (float)transformation.Mapping.Map(distance.R);
                output[i + 1] = (float)transformation.Mapping.Map(distance.G);
                output[i + 2] = (float)transformation.Mapping.Map(distance.B);
            }

            rightToLeft = !rightToLeft;
        }
    }

    /// <summary>
    ///     Splits the bitmap into horizontal bands across the thread pool.
    ///     <para>
    ///         Pixels are independent: the only state shared between them is the distance bound
    ///         each <see cref="ShapeDistanceFinder" /> carries for its early-out, and that only
    ///         decides how much work a pixel does, never what it comes out as. Banded output is
    ///         therefore bit-identical to serial. The <see cref="Shape" /> is only read from here,
    ///         so the bands can share it.
    ///     </para>
    /// </summary>
    private static unsafe void GenerateParallel(Span<float> output, int size, int channels, Shape shape,
        in SdfTransformation transformation, ConcurrentBag<ShapeDistanceFinder> finderPool)
    {
        var bandCount = (size + RowsPerBand - 1) / RowsPerBand;
        var edgeCount = shape.EdgeCount;
        var length = output.Length;

        // Span cannot cross into the lambda; the pointer is valid for the duration because
        // Parallel.For does not return until every band has finished.
        fixed (float* pinned = output)
        {
            var buffer = pinned;
            var localTransformation = transformation;

            Parallel.For(0, bandCount,
                () =>
                {
                    if (!finderPool.TryTake(out var local)) local = new ShapeDistanceFinder();
                    local.Reset(edgeCount);
                    return local;
                },
                (band, _, local) =>
                {
                    var startRow = band * RowsPerBand;
                    var endRow = Math.Min(startRow + RowsPerBand, size);

                    GenerateRows(new Span<float>(buffer, length), startRow, endRow, size, channels,
                        shape, localTransformation, local);

                    return local;
                },
                finderPool.Add);
        }
    }
}
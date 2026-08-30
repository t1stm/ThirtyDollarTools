using Sundex.MSDF.Geometry;

namespace Sundex.MSDF.Generation;

/// <summary>The span of signed distance, either side of the outline, that gets mapped onto 0..1.</summary>
internal readonly struct DistanceRange(double symmetricalWidth)
{
    public readonly double Lower = -.5 * symmetricalWidth;
    public readonly double Upper = .5 * symmetricalWidth;
}

/// <summary>
///     Turns a signed distance into a pixel value, with the outline itself landing on 0.5.
///     <para>
///         The range is in <b>shape units</b>, not output pixels. The engine passes 4.0 against an
///         em-normalised shape, so a glyph's field only occupies roughly 0.42..0.59 of the 0..1
///         span, and the shader's <c>uPxRange</c> stretches it back out. The two are coupled:
///         changing the range here without changing the uniform changes how wide the antialiasing
///         comes out.
///     </para>
/// </summary>
internal readonly struct DistanceMapping
{
    private readonly double _scale;
    private readonly double _translate;

    public DistanceMapping(DistanceRange range)
    {
        _scale = 1 / (range.Upper - range.Lower);
        _translate = -range.Lower;
    }

    public double Map(double distance)
    {
        return _scale * (distance + _translate);
    }
}

/// <summary>Converts between shape space and output pixel space.</summary>
internal readonly struct Projection(Vector2d scale, Vector2d translate)
{
    public Vector2d Project(Vector2d coord)
    {
        return Vector2d.Scale(scale, coord + translate);
    }

    public Vector2d Unproject(Vector2d coord)
    {
        return new Vector2d(coord.X / scale.X - translate.X, coord.Y / scale.Y - translate.Y);
    }
}

/// <summary>Everything needed to place a shape in a bitmap: where the pixels fall, and what the distances mean.</summary>
internal readonly struct SdfTransformation(Projection projection, DistanceMapping mapping)
{
    public readonly DistanceMapping Mapping = mapping;

    public Vector2d Unproject(Vector2d coord)
    {
        return projection.Unproject(coord);
    }
}
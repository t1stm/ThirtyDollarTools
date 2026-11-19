using System.Numerics;

namespace Sundex.Slug.Tests;

/// <summary>
///     A line-by-line C# port of <c>SlugRender</c> and <c>CalcCoverage</c> from Eric Lengyel's
///     reference <c>SlugPixelShader.hlsl</c> (MIT, Copyright 2017 Eric Lengyel), non-zero fill only
///     and without <c>SLUG_WEIGHT</c>. The engine's <c>Batched.frag</c> is the same function in GLSL,
///     so a fault in the glyph data shows up here rather than on screen.
///     <para>
///         Texel loads are array reads, <c>asuint</c> is <see cref="BitConverter.SingleToUInt32Bits" />,
///         and <c>pixelsPerEm</c> is a parameter rather than <c>1 / fwidth(renderCoord)</c>. A load past
///         the right edge of a row throws instead of reading whatever the GPU would.
///     </para>
/// </summary>
internal static class SlugReference
{
    private const int LogBandTextureWidth = 12;

    private static Vector4 TexelLoad2D(float[] texture, int x, int y)
    {
        if (x is < 0 or >= SlugFontData.TextureWidth)
            throw new IndexOutOfRangeException($"Texel ({x}, {y}) is off the end of its row.");

        var offset = (y * SlugFontData.TextureWidth + x) * 4;
        return new Vector4(texture[offset], texture[offset + 1], texture[offset + 2], texture[offset + 3]);
    }

    private static uint CalcRootCode(float y1, float y2, float y3)
    {
        var sign1 = BitConverter.SingleToUInt32Bits(y1) >> 31;
        var sign2 = BitConverter.SingleToUInt32Bits(y2) >> 30;
        var sign3 = BitConverter.SingleToUInt32Bits(y3) >> 29;

        var shift = (sign2 & 2U) | (sign1 & ~2U);
        shift = (sign3 & 4U) | (shift & ~4U);

        return (0x2E74U >> (int)shift) & 0x0101U;
    }

    private static Vector2 SolveHorizPoly(Vector4 point12, Vector2 point3)
    {
        var quadratic = new Vector2(point12.X, point12.Y) - new Vector2(point12.Z, point12.W) * 2f + point3;
        var linear = new Vector2(point12.X, point12.Y) - new Vector2(point12.Z, point12.W);
        var quadraticReciprocal = 1f / quadratic.Y;
        var linearReciprocal = 0.5f / linear.Y;

        var discriminant = MathF.Sqrt(MathF.Max(linear.Y * linear.Y - quadratic.Y * point12.Y, 0f));
        var root1 = (linear.Y - discriminant) * quadraticReciprocal;
        var root2 = (linear.Y + discriminant) * quadraticReciprocal;

        if (MathF.Abs(quadratic.Y) < 1f / 65536f) root1 = root2 = point12.Y * linearReciprocal;

        return new Vector2((quadratic.X * root1 - linear.X * 2f) * root1 + point12.X, (quadratic.X * root2 - linear.X * 2f) * root2 + point12.X);
    }

    private static Vector2 SolveVertPoly(Vector4 point12, Vector2 point3)
    {
        var quadratic = new Vector2(point12.X, point12.Y) - new Vector2(point12.Z, point12.W) * 2f + point3;
        var linear = new Vector2(point12.X, point12.Y) - new Vector2(point12.Z, point12.W);
        var quadraticReciprocal = 1f / quadratic.X;
        var linearReciprocal = 0.5f / linear.X;

        var discriminant = MathF.Sqrt(MathF.Max(linear.X * linear.X - quadratic.X * point12.X, 0f));
        var root1 = (linear.X - discriminant) * quadraticReciprocal;
        var root2 = (linear.X + discriminant) * quadraticReciprocal;

        if (MathF.Abs(quadratic.X) < 1f / 65536f) root1 = root2 = point12.X * linearReciprocal;

        return new Vector2((quadratic.Y * root1 - linear.Y * 2f) * root1 + point12.Y, (quadratic.Y * root2 - linear.Y * 2f) * root2 + point12.Y);
    }

    private static (int X, int Y) CalcBandLoc(int glyphLocX, int glyphLocY, uint offset)
    {
        var x = glyphLocX + (int)offset;
        var y = glyphLocY;
        y += x >> LogBandTextureWidth;
        x &= (1 << LogBandTextureWidth) - 1;
        return (x, y);
    }

    private static float Saturate(float x) => Math.Clamp(x, 0f, 1f);

    private static float CalcCoverage(float xcov, float ycov, float xwgt, float ywgt)
    {
        var coverage = MathF.Max(MathF.Abs(xcov * xwgt + ycov * ywgt) / MathF.Max(xwgt + ywgt, 1f / 65536f),
            MathF.Min(MathF.Abs(xcov), MathF.Abs(ycov)));

        return Saturate(coverage);
    }

    /// <summary>Coverage, 0..1, of the pixel centred on <paramref name="renderCoord" /> (in ems).</summary>
    public static float SlugRender(SlugFontData data, SlugGlyph glyph, Vector2 renderCoord, Vector2 pixelsPerEm)
    {
        var curveData = data.Curves;
        var bandData = data.Bands;
        var bandTransform = glyph.Banding;
        var glyphData = glyph.GlyphData;

        int curveIndex;

        int bandMaxX = (int)glyphData.Z, bandMaxY = (int)glyphData.W;

        var bandIndexX = Math.Clamp((int)(renderCoord.X * bandTransform.X + bandTransform.Z), 0, bandMaxX);
        var bandIndexY = Math.Clamp((int)(renderCoord.Y * bandTransform.Y + bandTransform.W), 0, bandMaxY);
        int glyphLocX = (int)glyphData.X, glyphLocY = (int)glyphData.Y;

        var xcov = 0f;
        var xwgt = 0f;

        var hbandData = TexelLoad2D(bandData, glyphLocX + bandIndexY, glyphLocY);
        var hbandLoc = CalcBandLoc(glyphLocX, glyphLocY, (uint)hbandData.Y);

        for (curveIndex = 0; curveIndex < (int)hbandData.X; curveIndex++)
        {
            var curveTexel = TexelLoad2D(bandData, hbandLoc.X + curveIndex, hbandLoc.Y);
            int curveLocX = (int)curveTexel.X, curveLocY = (int)curveTexel.Y;

            var point12 = TexelLoad2D(curveData, curveLocX, curveLocY) -
                      new Vector4(renderCoord.X, renderCoord.Y, renderCoord.X, renderCoord.Y);
            var point3 = Xy(TexelLoad2D(curveData, curveLocX + 1, curveLocY)) - renderCoord;

            if (MathF.Max(MathF.Max(point12.X, point12.Z), point3.X) * pixelsPerEm.X < -0.5f) break;

            var code = CalcRootCode(point12.Y, point12.W, point3.Y);
            if (code != 0U)
            {
                var roots = SolveHorizPoly(point12, point3) * pixelsPerEm.X;

                if ((code & 1U) != 0U)
                {
                    xcov += Saturate(roots.X + 0.5f);
                    xwgt = MathF.Max(xwgt, Saturate(1f - MathF.Abs(roots.X) * 2f));
                }

                if (code > 1U)
                {
                    xcov -= Saturate(roots.Y + 0.5f);
                    xwgt = MathF.Max(xwgt, Saturate(1f - MathF.Abs(roots.Y) * 2f));
                }
            }
        }

        var ycov = 0f;
        var ywgt = 0f;

        var vbandData = TexelLoad2D(bandData, glyphLocX + bandMaxY + 1 + bandIndexX, glyphLocY);
        var vbandLoc = CalcBandLoc(glyphLocX, glyphLocY, (uint)vbandData.Y);

        for (curveIndex = 0; curveIndex < (int)vbandData.X; curveIndex++)
        {
            var curveTexel = TexelLoad2D(bandData, vbandLoc.X + curveIndex, vbandLoc.Y);
            int curveLocX = (int)curveTexel.X, curveLocY = (int)curveTexel.Y;

            var point12 = TexelLoad2D(curveData, curveLocX, curveLocY) -
                      new Vector4(renderCoord.X, renderCoord.Y, renderCoord.X, renderCoord.Y);
            var point3 = Xy(TexelLoad2D(curveData, curveLocX + 1, curveLocY)) - renderCoord;

            if (MathF.Max(MathF.Max(point12.Y, point12.W), point3.Y) * pixelsPerEm.Y < -0.5f) break;

            var code = CalcRootCode(point12.X, point12.Z, point3.X);
            if (code != 0U)
            {
                var roots = SolveVertPoly(point12, point3) * pixelsPerEm.Y;

                if ((code & 1U) != 0U)
                {
                    ycov -= Saturate(roots.X + 0.5f);
                    ywgt = MathF.Max(ywgt, Saturate(1f - MathF.Abs(roots.X) * 2f));
                }

                if (code > 1U)
                {
                    ycov += Saturate(roots.Y + 0.5f);
                    ywgt = MathF.Max(ywgt, Saturate(1f - MathF.Abs(roots.Y) * 2f));
                }
            }
        }

        return CalcCoverage(xcov, ycov, xwgt, ywgt);
    }

    private static Vector2 Xy(Vector4 vector) => new(vector.X, vector.Y);
}

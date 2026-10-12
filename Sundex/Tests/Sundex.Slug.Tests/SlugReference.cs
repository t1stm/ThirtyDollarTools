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

        var i = (y * SlugFontData.TextureWidth + x) * 4;
        return new Vector4(texture[i], texture[i + 1], texture[i + 2], texture[i + 3]);
    }

    private static uint CalcRootCode(float y1, float y2, float y3)
    {
        var i1 = BitConverter.SingleToUInt32Bits(y1) >> 31;
        var i2 = BitConverter.SingleToUInt32Bits(y2) >> 30;
        var i3 = BitConverter.SingleToUInt32Bits(y3) >> 29;

        var shift = (i2 & 2U) | (i1 & ~2U);
        shift = (i3 & 4U) | (shift & ~4U);

        return (0x2E74U >> (int)shift) & 0x0101U;
    }

    private static Vector2 SolveHorizPoly(Vector4 p12, Vector2 p3)
    {
        var a = new Vector2(p12.X, p12.Y) - new Vector2(p12.Z, p12.W) * 2f + p3;
        var b = new Vector2(p12.X, p12.Y) - new Vector2(p12.Z, p12.W);
        var ra = 1f / a.Y;
        var rb = 0.5f / b.Y;

        var d = MathF.Sqrt(MathF.Max(b.Y * b.Y - a.Y * p12.Y, 0f));
        var t1 = (b.Y - d) * ra;
        var t2 = (b.Y + d) * ra;

        if (MathF.Abs(a.Y) < 1f / 65536f) t1 = t2 = p12.Y * rb;

        return new Vector2((a.X * t1 - b.X * 2f) * t1 + p12.X, (a.X * t2 - b.X * 2f) * t2 + p12.X);
    }

    private static Vector2 SolveVertPoly(Vector4 p12, Vector2 p3)
    {
        var a = new Vector2(p12.X, p12.Y) - new Vector2(p12.Z, p12.W) * 2f + p3;
        var b = new Vector2(p12.X, p12.Y) - new Vector2(p12.Z, p12.W);
        var ra = 1f / a.X;
        var rb = 0.5f / b.X;

        var d = MathF.Sqrt(MathF.Max(b.X * b.X - a.X * p12.X, 0f));
        var t1 = (b.X - d) * ra;
        var t2 = (b.X + d) * ra;

        if (MathF.Abs(a.X) < 1f / 65536f) t1 = t2 = p12.X * rb;

        return new Vector2((a.Y * t1 - b.Y * 2f) * t1 + p12.Y, (a.Y * t2 - b.Y * 2f) * t2 + p12.Y);
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

            var p12 = TexelLoad2D(curveData, curveLocX, curveLocY) -
                      new Vector4(renderCoord.X, renderCoord.Y, renderCoord.X, renderCoord.Y);
            var p3 = Xy(TexelLoad2D(curveData, curveLocX + 1, curveLocY)) - renderCoord;

            if (MathF.Max(MathF.Max(p12.X, p12.Z), p3.X) * pixelsPerEm.X < -0.5f) break;

            var code = CalcRootCode(p12.Y, p12.W, p3.Y);
            if (code != 0U)
            {
                var r = SolveHorizPoly(p12, p3) * pixelsPerEm.X;

                if ((code & 1U) != 0U)
                {
                    xcov += Saturate(r.X + 0.5f);
                    xwgt = MathF.Max(xwgt, Saturate(1f - MathF.Abs(r.X) * 2f));
                }

                if (code > 1U)
                {
                    xcov -= Saturate(r.Y + 0.5f);
                    xwgt = MathF.Max(xwgt, Saturate(1f - MathF.Abs(r.Y) * 2f));
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

            var p12 = TexelLoad2D(curveData, curveLocX, curveLocY) -
                      new Vector4(renderCoord.X, renderCoord.Y, renderCoord.X, renderCoord.Y);
            var p3 = Xy(TexelLoad2D(curveData, curveLocX + 1, curveLocY)) - renderCoord;

            if (MathF.Max(MathF.Max(p12.Y, p12.W), p3.Y) * pixelsPerEm.Y < -0.5f) break;

            var code = CalcRootCode(p12.X, p12.Z, p3.X);
            if (code != 0U)
            {
                var r = SolveVertPoly(p12, p3) * pixelsPerEm.Y;

                if ((code & 1U) != 0U)
                {
                    ycov -= Saturate(r.X + 0.5f);
                    ywgt = MathF.Max(ywgt, Saturate(1f - MathF.Abs(r.X) * 2f));
                }

                if (code > 1U)
                {
                    ycov += Saturate(r.Y + 0.5f);
                    ywgt = MathF.Max(ywgt, Saturate(1f - MathF.Abs(r.Y) * 2f));
                }
            }
        }

        return CalcCoverage(xcov, ycov, xwgt, ywgt);
    }

    private static Vector2 Xy(Vector4 v) => new(v.X, v.Y);
}

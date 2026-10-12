#version 330 core

// ===================================================
// Reference pixel shader for the Slug algorithm.
// This code is made available under the MIT License.
// Copyright 2017, by Eric Lengyel.
// ===================================================

// Translated to GLSL 330 from SlugPixelShader.hlsl in github.com/EricLengyel/Slug at be3c13e.
// Non-zero fill only, no SLUG_WEIGHT, and no flags in glyphData. The band texture is RGBA32F
// rather than an integer format, so its texels are read as floats and converted. The derivatives
// are taken in main, before the clip discard, and passed in. Sundex.Slug.Tests/SlugReference.cs
// is the same function in C#, checked against a ground-truth rasteriser there.

// The curve and band textures use a fixed width of 4096 texels.

#define kLogBandTextureWidth 12

uniform sampler2D uCurves;
uniform sampler2D uBands;

in vec2 vEmCoord;
in vec4 vColor;
in vec2 vUIPosition;
flat in vec4 vClipRect;
flat in vec4 vBanding;
flat in vec4 vGlyph;

out vec4 outColor;

uint CalcRootCode(float y1, float y2, float y3)
{
    // Calculate the root eligibility code for a sample-relative quadratic Bezier curve.
    // Extract the signs of the y coordinates of the three control points.

    uint i1 = floatBitsToUint(y1) >> 31U;
    uint i2 = floatBitsToUint(y2) >> 30U;
    uint i3 = floatBitsToUint(y3) >> 29U;

    uint shift = (i2 & 2U) | (i1 & ~2U);
    shift = (i3 & 4U) | (shift & ~4U);

    // Eligibility is returned in bits 0 and 8.

    return ((0x2E74U >> shift) & 0x0101U);
}

vec2 SolveHorizPoly(vec4 p12, vec2 p3)
{
    // Solve for the values of t where the curve crosses y = 0.
    // The quadratic polynomial in t is given by
    //
    //     a t^2 - 2b t + c,
    //
    // where a = p1.y - 2 p2.y + p3.y, b = p1.y - p2.y, and c = p1.y.
    // The discriminant b^2 - ac is clamped to zero, and imaginary
    // roots are treated as a double root at the global minimum
    // where t = b / a.

    vec2 a = p12.xy - p12.zw * 2.0 + p3;
    vec2 b = p12.xy - p12.zw;
    float ra = 1.0 / a.y;
    float rb = 0.5 / b.y;

    float d = sqrt(max(b.y * b.y - a.y * p12.y, 0.0));
    float t1 = (b.y - d) * ra;
    float t2 = (b.y + d) * ra;

    // If the polynomial is nearly linear, then solve -2b t + c = 0.

    if (abs(a.y) < 1.0 / 65536.0) t1 = t2 = p12.y * rb;

    // Return the x coordinates where C(t) = 0.

    return (vec2((a.x * t1 - b.x * 2.0) * t1 + p12.x, (a.x * t2 - b.x * 2.0) * t2 + p12.x));
}

vec2 SolveVertPoly(vec4 p12, vec2 p3)
{
    // Solve for the values of t where the curve crosses x = 0.

    vec2 a = p12.xy - p12.zw * 2.0 + p3;
    vec2 b = p12.xy - p12.zw;
    float ra = 1.0 / a.x;
    float rb = 0.5 / b.x;

    float d = sqrt(max(b.x * b.x - a.x * p12.x, 0.0));
    float t1 = (b.x - d) * ra;
    float t2 = (b.x + d) * ra;

    // If the polynomial is nearly linear, then solve -2b t + c = 0.

    if (abs(a.x) < 1.0 / 65536.0) t1 = t2 = p12.x * rb;

    // Return the y coordinates where C(t) = 0.

    return (vec2((a.y * t1 - b.y * 2.0) * t1 + p12.y, (a.y * t2 - b.y * 2.0) * t2 + p12.y));
}

ivec2 CalcBandLoc(ivec2 glyphLoc, uint offset)
{
    // If the offset causes the x coordinate to exceed the texture width, then wrap to the next line.

    ivec2 bandLoc = ivec2(glyphLoc.x + int(offset), glyphLoc.y);
    bandLoc.y += bandLoc.x >> kLogBandTextureWidth;
    bandLoc.x &= (1 << kLogBandTextureWidth) - 1;
    return (bandLoc);
}

float CalcCoverage(float xcov, float ycov, float xwgt, float ywgt)
{
    // Combine coverages from the horizontal and vertical rays using their weights.
    // Absolute values ensure that either winding direction convention works.

    float coverage = max(abs(xcov * xwgt + ycov * ywgt) / max(xwgt + ywgt, 1.0 / 65536.0), min(abs(xcov), abs(ycov)));

    // Using nonzero fill rule here.

    return (clamp(coverage, 0.0, 1.0));
}

float SlugRender(vec2 renderCoord, vec2 pixelsPerEm, vec4 bandTransform, ivec4 glyphData)
{
    int curveIndex;

    ivec2 bandMax = glyphData.zw;

    // Determine what bands the current pixel lies in by applying a scale and offset
    // to the render coordinates. The scales are given by bandTransform.xy, and the
    // offsets are given by bandTransform.zw. Band indexes are clamped to [0, bandMax.xy].

    ivec2 bandIndex = clamp(ivec2(renderCoord * bandTransform.xy + bandTransform.zw), ivec2(0, 0), bandMax);
    ivec2 glyphLoc = glyphData.xy;

    float xcov = 0.0;
    float xwgt = 0.0;

    // Fetch data for the horizontal band from the index texture. The number
    // of curves intersecting the band is in the x component, and the offset
    // to the list of locations for those curves is in the y component.

    uvec2 hbandData = uvec2(texelFetch(uBands, ivec2(glyphLoc.x + bandIndex.y, glyphLoc.y), 0).xy);
    ivec2 hbandLoc = CalcBandLoc(glyphLoc, hbandData.y);

    // Loop over all curves in the horizontal band.

    for (curveIndex = 0; curveIndex < int(hbandData.x); curveIndex++)
    {
        // Fetch the location of the current curve from the index texture.

        ivec2 curveLoc = ivec2(texelFetch(uBands, ivec2(hbandLoc.x + curveIndex, hbandLoc.y), 0).xy);

        // Fetch the three 2D control points for the current curve from the curve texture.
        // The first texel contains both p1 and p2 in the (x,y) and (z,w) components, respectively,
        // and the the second texel contains p3 in the (x,y) components. Subtracting the render
        // coordinates makes the curve relative to the sample position. The quadratic Bezier curve
        // C(t) is given by
        //
        //     C(t) = (1 - t)^2 p1 + 2t(1 - t) p2 + t^2 p3

        vec4 p12 = texelFetch(uCurves, curveLoc, 0) - vec4(renderCoord, renderCoord);
        vec2 p3 = texelFetch(uCurves, ivec2(curveLoc.x + 1, curveLoc.y), 0).xy - renderCoord;

        // If the largest x coordinate among all three control points falls
        // left of the current pixel, then there are no more curves in the
        // horizontal band that can influence the result, so exit the loop.
        // (The curves are sorted in descending order by max x coordinate.)

        if (max(max(p12.x, p12.z), p3.x) * pixelsPerEm.x < -0.5) break;

        uint code = CalcRootCode(p12.y, p12.w, p3.y);
        if (code != 0U)
        {
            // At least one root makes a contribution. Calculate them and scale so
            // that the current pixel corresponds to the range [0,1].

            vec2 r = SolveHorizPoly(p12, p3) * pixelsPerEm.x;

            // Bits in code tell which roots make a contribution.

            if ((code & 1U) != 0U)
            {
                xcov += clamp(r.x + 0.5, 0.0, 1.0);
                xwgt = max(xwgt, clamp(1.0 - abs(r.x) * 2.0, 0.0, 1.0));
            }

            if (code > 1U)
            {
                xcov -= clamp(r.y + 0.5, 0.0, 1.0);
                xwgt = max(xwgt, clamp(1.0 - abs(r.y) * 2.0, 0.0, 1.0));
            }
        }
    }

    float ycov = 0.0;
    float ywgt = 0.0;

    // Fetch data for the vertical band from the index texture. This follows
    // the data for all horizontal bands, so we have to add bandMax.y + 1.

    uvec2 vbandData = uvec2(texelFetch(uBands, ivec2(glyphLoc.x + bandMax.y + 1 + bandIndex.x, glyphLoc.y), 0).xy);
    ivec2 vbandLoc = CalcBandLoc(glyphLoc, vbandData.y);

    // Loop over all curves in the vertical band.

    for (curveIndex = 0; curveIndex < int(vbandData.x); curveIndex++)
    {
        ivec2 curveLoc = ivec2(texelFetch(uBands, ivec2(vbandLoc.x + curveIndex, vbandLoc.y), 0).xy);
        vec4 p12 = texelFetch(uCurves, curveLoc, 0) - vec4(renderCoord, renderCoord);
        vec2 p3 = texelFetch(uCurves, ivec2(curveLoc.x + 1, curveLoc.y), 0).xy - renderCoord;

        // If the largest y coordinate among all three control points falls
        // below the current pixel, then there are no more curves in the
        // vertical band that can influence the result, so exit the loop.
        // (The curves are sorted in descending order by max y coordinate.)

        if (max(max(p12.y, p12.w), p3.y) * pixelsPerEm.y < -0.5) break;

        uint code = CalcRootCode(p12.x, p12.z, p3.x);
        if (code != 0U)
        {
            vec2 r = SolveVertPoly(p12, p3) * pixelsPerEm.y;

            if ((code & 1U) != 0U)
            {
                ycov -= clamp(r.x + 0.5, 0.0, 1.0);
                ywgt = max(ywgt, clamp(1.0 - abs(r.x) * 2.0, 0.0, 1.0));
            }

            if (code > 1U)
            {
                ycov += clamp(r.y + 0.5, 0.0, 1.0);
                ywgt = max(ywgt, clamp(1.0 - abs(r.y) * 2.0, 0.0, 1.0));
            }
        }
    }

    return (CalcCoverage(xcov, ycov, xwgt, ywgt));
}

void main() {
    // The effective pixel dimensions of the em square are computed
    // independently for x and y directions with texcoord derivatives.

    vec2 pixelsPerEm = 1.0 / fwidth(vEmCoord);

    // An all-zero rect is "unclipped" - the default every glyph that never asked for a
    // box carries. Otherwise (left, top, right, bottom) in UI units; a rect whose edges
    // cross hides the glyph outright, which is what a box too narrow for text wants.
    if (vClipRect != vec4(0.0) &&
        (vUIPosition.x < vClipRect.x || vUIPosition.x > vClipRect.z ||
         vUIPosition.y < vClipRect.y || vUIPosition.y > vClipRect.w)) discard;

    float coverage = SlugRender(vEmCoord, pixelsPerEm, vBanding, ivec4(vGlyph));
    outColor = vec4(vColor.rgb, vColor.a * coverage);
}

#version 330 core

// A CSS box: fill, per-side border, radius, a radial glow, and optionally the frosted backdrop
// behind it. What ColoredPlane has no way to draw; a browser gets all of this from one rule.

out vec4 color;

in vec2 vLocalPos;
in vec2 vUv;

uniform sampler2D uBackdrop;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;
    vec4 u_Color;
    vec4 u_BorderColor;
    vec4 u_Border;            // top, right, bottom, left
    vec4 u_ScaleRadiusLod;    // width, height, radius, backdrop lod (negative: no backdrop)
    vec4 u_GlowColor;
    vec4 u_Glow;              // centre x, centre y, radius x, radius y; all fractions of the box
    vec4 u_UvRect;
};

float roundedBoxSDF(vec2 p, vec2 b, float r) {
    vec2 d = abs(p) - b + r;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
}

void main() {
    vec2 size = u_ScaleRadiusLod.xy;
    float radius = min(u_ScaleRadiusLod.z, min(size.x, size.y) * 0.5);

    vec4 fill = u_Color;

    if (u_ScaleRadiusLod.w >= 0.0) {
        vec2 uv = u_UvRect.xy + vUv * u_UvRect.zw;
        uv.y = u_UvRect.y + (1.0 - vUv.y) * u_UvRect.w;
        vec4 behind = textureLod(uBackdrop, uv, u_ScaleRadiusLod.w);
        fill = vec4(mix(behind.rgb, u_Color.rgb, u_Color.a), 1.0);
    }

    if (u_GlowColor.a > 0.0) {
        vec2 uv = vec2(vUv.x, vUv.y);
        float t = length((uv - u_Glow.xy) / u_Glow.zw);
        float falloff = 1.0 - clamp(t / 0.62, 0.0, 1.0);
        fill.rgb = mix(fill.rgb, u_GlowColor.rgb, u_GlowColor.a * falloff);
        fill.a = max(fill.a, u_GlowColor.a * falloff);
    }

    float outer = roundedBoxSDF(vLocalPos, size * 0.5, radius);
    float pixel = max(length(vec2(dFdx(vLocalPos.x), dFdy(vLocalPos.y))) * 0.7071, 1e-4);

    vec4 border = u_Border;
    if (border.x > 0.0 || border.y > 0.0 || border.z > 0.0 || border.w > 0.0) {
        vec2 innerHalf = max((size - vec2(border.w + border.y, border.x + border.z)) * 0.5, vec2(0.0));
        vec2 innerCentre = vec2(border.w - border.y, border.x - border.z) * 0.5;
        float innerRadius = max(radius - max(max(border.x, border.y), max(border.z, border.w)), 0.0);
        float inner = roundedBoxSDF(vLocalPos - innerCentre, innerHalf, innerRadius);
        float edge = clamp(0.5 + inner / pixel, 0.0, 1.0);
        fill = mix(fill, u_BorderColor, edge);
    }

    color = fill;
    if (radius > 0.0) color.a *= clamp(0.5 - outer / pixel, 0.0, 1.0);
}

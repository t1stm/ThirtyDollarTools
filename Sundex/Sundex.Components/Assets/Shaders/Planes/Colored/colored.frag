#version 330 core

out vec4 color;

in vec2 vLocalPos;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;

    vec4 u_Color;
    vec4 u_ScaleAndBorderPx;
};

float roundedBoxSDF(vec2 p, vec2 b, float r) {
    vec2 d = abs(p) - b + r;
    return length(max(d, 0.0)) - r;
}

void main() {
    color = u_Color;

    float borderRadius = u_ScaleAndBorderPx.z;
    if (borderRadius > 0.0) {
        float dist = roundedBoxSDF(vLocalPos, u_ScaleAndBorderPx.xy * 0.5, borderRadius);
        // Coverage centred on the edge, one pixel wide: half covered on the boundary, as a
        // browser draws it. Fading out only past the edge grew the shape by up to a pixel and a
        // half, most on the diagonal (fwidth is |dx| + |dy|), which flattened the corners.
        // The gradient's length is how far dist moves per screen pixel, at any pixel scale.
        float pixel = length(vec2(dFdx(dist), dFdy(dist)));
        float alpha = clamp(0.5 - dist / max(pixel, 1e-4), 0.0, 1.0);
        color.a *= alpha;
    }
}
#version 330 core

out vec4 color;

in vec2 vLocalPos;
in vec2 vUV;

uniform sampler2D uTexture;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;
    vec4 u_ScaleAndBorderPx;
};

float roundedBoxSDF(vec2 p, vec2 b, float r) {
    vec2 d = abs(p) - b + r;
    return length(max(d, 0.0)) - r;
}

void main() {
    color = texture(uTexture, vUV);
    float borderRadius = u_ScaleAndBorderPx.z;

    if (borderRadius > 0.0) {
        float dist = roundedBoxSDF(vLocalPos, u_ScaleAndBorderPx.xy * 0.5, borderRadius);
        float edge = fwidth(dist);
        float alpha = 1.0 - smoothstep(0.0, edge, dist);
        color.a *= alpha;
    }
}
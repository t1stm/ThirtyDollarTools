#version 330 core

in vec2 fragment_coords;
out vec4 color;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;

    vec4 u_Color;
    vec3 u_PositionPx;
    vec3 u_ScalePx;
    float u_BorderRadiusPx;
};

float roundedBoxSDF(vec2 center, vec2 size, float radius) {
    return length(max(abs(center) - size + radius, 0.0)) - radius;
}

void main() {
    if (u_BorderRadiusPx == 0.0) {
        color = u_Color;
        return;
    }

    float edgeSoftness = 0.5f;

    vec2 lower_left = vec2(u_PositionPx.x, u_PositionPx.y - u_ScalePx.y);

    float distance = roundedBoxSDF(gl_FragCoord.xy - lower_left - (u_ScalePx.xy / 2.0f), u_ScalePx.xy / 2.0f, u_BorderRadiusPx);
    float smoothedAlpha = 1.0f - smoothstep(0.0f, edgeSoftness * 2.0f, distance);
    color = vec4(u_Color.rgb, smoothedAlpha);
}
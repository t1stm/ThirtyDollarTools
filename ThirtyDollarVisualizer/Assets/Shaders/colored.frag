#version 330 core

in vec2 fragment_coords;
out vec4 color;

uniform vec4 u_Color;
uniform vec3 u_PositionPx;
uniform vec3 u_ScalePx;
uniform float u_BorderRadiusPx;

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
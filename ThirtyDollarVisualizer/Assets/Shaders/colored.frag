#version 330 core

in vec2 fragment_coords;
out vec4 color;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;

    vec4 u_Color;
    float u_ScalePx;
    float u_AspectRatio;
    float u_BorderRadiusPx;
    vec4 u_Viewport;
};

float roundedBoxSDF(vec2 center, vec2 size, float radius) {
    return length(max(abs(center) - size + radius, 0.0)) - radius;
}

vec3 getScale(mat4 model) {
    float scaleX = length(model[0].xyz);
    float scaleY = length(model[1].xyz);
    float scaleZ = length(model[2].xyz);
    return vec3(scaleX, scaleY, scaleZ);
}

void main() {
    if (u_BorderRadiusPx == 0.0) {
        color = u_Color;
        return;
    }

    vec4 coords = u_Projection * u_Model * vec4(fragment_coords, 1, 1);
    float edgeSoftness = 0.5f;

    vec2 scalePx = vec2(
    u_ScalePx,
    u_ScalePx / u_AspectRatio
    );
    vec2 lower_left = vec2(coords.x, coords.y - scalePx.y);

    float distance = roundedBoxSDF(gl_FragCoord.xy - lower_left - (scalePx.xy / 2.0f), scalePx.xy / 2.0f, u_BorderRadiusPx);
    float smoothedAlpha = 1.0f - smoothstep(0.0f, edgeSoftness * 2.0f, distance);
    color = vec4(u_Color.rgb, smoothedAlpha);
}
#version 330 core
#define MAX_STOPS 8

layout (location = 0) in vec3 aPosition;

out vec2 vLocalPos;
out vec2 vUV;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;
    vec4 u_ScaleAndBorderPx;

    int u_GradientType;
    int u_GradientStopCount;

    vec4 u_GradientColors[MAX_STOPS];
    float u_GradientStops[MAX_STOPS];
};

void main() {
    vUV = aPosition.xy;
    vLocalPos = (vUV - 0.5) * u_ScaleAndBorderPx.xy;

    vec4 final_coords = u_Projection * u_Model * vec4(aPosition, 1.0);
    gl_Position = final_coords;
}

#version 330 core

layout (location = 0) in vec3 aPosition;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;

    vec4 u_Color;
    float u_ScalePx;
    float u_AspectRatio;
    float u_BorderRadiusPx;
    vec4 u_Viewport;
};

void main() {
    vec4 final_coords = u_Projection * u_Model * vec4(aPosition, 1.0);
    gl_Position = final_coords;
}

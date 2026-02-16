#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aUV;

out vec2 vLocalPos;
out vec2 vUV;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;
    vec4 u_ScaleAndBorderPx;
};

void main() {
    vLocalPos = (aPosition.xy - 0.5) * u_ScaleAndBorderPx.xy;
    vUV = aUV;
    vec4 final_coords = u_Projection * u_Model * vec4(aPosition, 1.0);
    gl_Position = final_coords;
}

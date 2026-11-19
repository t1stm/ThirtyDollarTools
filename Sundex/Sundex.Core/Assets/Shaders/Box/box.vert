#version 330 core

layout (location = 0) in vec3 aPosition;

out vec2 vLocalPos;
out vec2 vUv;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;
    vec4 u_Color;
    vec4 u_BorderColor;
    vec4 u_Border;
    vec4 u_ScaleRadiusLod;
    vec4 u_GlowColor;
    vec4 u_Glow;
    vec4 u_UvRect;
};

void main() {
    vLocalPos = (aPosition.xy - 0.5) * u_ScaleRadiusLod.xy;
    vUv = aPosition.xy;
    gl_Position = u_Projection * u_Model * vec4(aPosition, 1.0);
}

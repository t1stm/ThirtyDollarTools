#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aTextureCoordinates;

out vec2 fragment_coords;

layout (std140) uniform Data {
    mat4 u_Model;
    mat4 u_Projection;

    float u_DeltaAlpha;
};

void main() {
    vec4 final_coords = u_Projection * u_Model * vec4(aPosition, 1.0);

    gl_Position = final_coords;
    fragment_coords = aTextureCoordinates;
}
